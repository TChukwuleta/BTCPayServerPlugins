using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using System.Collections.Generic;
using BTCPayServer.Services.PaymentRequests;
using Newtonsoft.Json.Linq;
using BTCPayServer.Services.Mails;
using BTCPayServer.Client.Models;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase
{
    private readonly EmailService _emailService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly GhostPluginService _ghostPluginService;

    public GhostHostedService(EmailService emailService,
        EventAggregator eventAggregator,
        EmailSenderFactory emailSenderFactory,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory httpClientFactory,
        GhostPluginService ghostPluginService,
        GhostDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _emailService = emailService;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClientFactory = httpClientFactory;
        _emailSenderFactory = emailSenderFactory;
        _ghostPluginService = ghostPluginService;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        Subscribe<PaymentRequestEvent>();
        base.SubscribeToEvents();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case InvoiceEvent invoiceEvent when new[]
            {
            InvoiceEvent.MarkedCompleted,
            InvoiceEvent.MarkedInvalid,
            InvoiceEvent.Expired,
            InvoiceEvent.Confirmed,
            InvoiceEvent.Completed
        }.Contains(invoiceEvent.Name):
                {
                    var invoice = invoiceEvent.Invoice;
                    var ghostOrderId = invoice.GetInternalTags(GhostApp.GHOST_PREFIX).FirstOrDefault();
                    if (ghostOrderId != null)
                    {
                        bool? success = invoice.Status switch
                        {
                            InvoiceStatus.Settled => true,

                            InvoiceStatus.Invalid or
                            InvoiceStatus.Expired => false,

                            _ => (bool?)null
                        };
                        if (success.HasValue)
                        {
                            if (ghostOrderId.StartsWith(GhostApp.GHOST_MEMBER_ID_PREFIX))
                            {
                                await RegisterMembershipCreationTransaction(invoice, ghostOrderId, success.Value);
                            }
                        }
                    }
                    break;
                }

            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged, Data.Status: Client.Models.PaymentRequestData.PaymentRequestStatus.Completed } paymentRequestStatusUpdated:
                {
                    var prBlob = paymentRequestStatusUpdated.Data.GetBlob();
                    if (!prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestSourceKey, out var src) ||
                        src?.Value<string>() != GhostApp.AppName || !prBlob.AdditionalData.TryGetValue(GhostApp.MemberIdKey, out var memberIdToken))
                    {
                        return;
                    }
                    var memberId = memberIdToken.Value<string>();
                    var memberEmail = prBlob.Email;
                    await _ghostPluginService.HandlePaidMembershipSubscription(prBlob, memberId, paymentRequestStatusUpdated.Data.Id, memberEmail);
                    break;
                }

        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterMembershipCreationTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId);

        if (ghostSetting.CredentialsPopulated())
        {
            var result = new InvoiceLogs();

            result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
            var transaction = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id);
            if (transaction == null)
            {
                result.Write("Couldn't find a corresponding Ghost transaction table record", InvoiceEventData.EventSeverity.Error);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
                return;
            }
            if (transaction != null && transaction.TransactionStatus != TransactionStatus.New)
            {
                result.Write("Transaction has previously been completed", InvoiceEventData.EventSeverity.Info);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
                return;
            }

            transaction.InvoiceStatus = invoice.Status.ToString().ToLower();
            transaction.TransactionStatus = success ? TransactionStatus.Settled : TransactionStatus.Expired;
            if (success)
            {
                try
                {
                    var ghostMember = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == transaction.MemberId);
                    var expirationDate = ghostMember.Frequency == TierSubscriptionFrequency.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1);
                    var client = new GhostAdminApiClient(_httpClientFactory, ghostSetting.CreateGhsotApiCredentials());
                    var response = await client.CreateGhostMember(new CreateGhostMemberRequest
                    {
                        members = new List<Member>
                        {
                            new Member
                            {
                                email = ghostMember.Email,
                                name = ghostMember.Name,
                                comped = false,
                                tiers = new List<MemberTier>
                                {
                                    new MemberTier
                                    {
                                        id = ghostMember.TierId,
                                        expiry_at = expirationDate
                                    }
                                }
                            }
                        }
                    });
                    transaction.PeriodStart = DateTime.UtcNow;
                    transaction.PeriodEnd = expirationDate;
                    ghostMember.MemberId = response.members[0].id;
                    ghostMember.MemberUuid = response.members[0].uuid;
                    ghostMember.UnsubscribeUrl = response.members[0].unsubscribe_url;
                    ghostMember.MemberId = response.members[0].id;
                    ctx.UpdateRange(ghostMember);
                    result.Write($"Successfully created member with name: {ghostMember.Name} on Ghost.", InvoiceEventData.EventSeverity.Info);
                }
                catch (Exception ex)
                {
                    Logs.PayServer.LogError(ex,
                        $"Shopify error while trying to create member on Ghost platfor. " +
                        $"Triggered by invoiceId: {invoice.Id}");
                }

            }
            ctx.UpdateRange(transaction);
            await ctx.SaveChangesAsync();
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
        }
    }

}

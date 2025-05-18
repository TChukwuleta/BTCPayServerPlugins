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
using BTCPayServer.Client.Models;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly GhostPluginService _ghostPluginService;

    public GhostHostedService(EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory httpClientFactory,
        GhostPluginService ghostPluginService,
        GhostDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClientFactory = httpClientFactory;
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
                        if (success.HasValue && ghostOrderId.StartsWith(GhostApp.GHOST_MEMBER_ID_PREFIX))
                        {
                            await RegisterMembershipCreationTransaction(invoice, ghostOrderId, success.Value);
                        }
                    }
                    break;
                }

            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged, Data.Status: PaymentRequestStatus.Completed } paymentRequestStatusUpdated:
                {
                    var prBlob = paymentRequestStatusUpdated.Data.GetBlob();

                    await using var ctx = _dbContextFactory.CreateContext();
                    var paymentRequestTransaction = ctx.GhostTransactions.FirstOrDefault(c => c.StoreId == paymentRequestStatusUpdated.Data.StoreDataId 
                        && c.PaymentRequestId == paymentRequestStatusUpdated.Data.Id);

                    if (paymentRequestTransaction == null) return;

                    await _ghostPluginService.HandlePaidMembershipSubscription(paymentRequestTransaction.MemberId, paymentRequestStatusUpdated.Data.Id, prBlob.Email);
                    break;
                }

        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterMembershipCreationTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        var result = new InvoiceLogs();

        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId);

        if (ghostSetting.CredentialsPopulated())
        {
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
                                tiers = new List<MemberTier>
                                {
                                    new MemberTier
                                    {
                                        id = ghostMember.TierId,
                                        expiry_at = expirationDate.ToUniversalTime()
                                    }
                                }
                            }
                        }
                    });
                    if (response == null || response.members == null || response.members.Count == 0 || response.members[0] == null)
                    {
                        result.Write($"Ghost response was null or empty when trying to create member", InvoiceEventData.EventSeverity.Error);
                        return;
                    }
                    transaction.PeriodStart = DateTime.UtcNow;
                    transaction.PeriodEnd = expirationDate.ToUniversalTime();
                    ghostMember.MemberId = response.members[0].id;
                    ghostMember.MemberUuid = response.members[0].uuid;
                    ghostMember.UnsubscribeUrl = response.members[0].unsubscribe_url;
                    ctx.UpdateRange(ghostMember);
                    result.Write($"Successfully created member with name: {ghostMember.Name} on Ghost.", InvoiceEventData.EventSeverity.Info);
                }
                catch (Exception ex)
                {
                    result.Write($"Ghost error while trying to create member on Ghost platform. {ex.Message}", InvoiceEventData.EventSeverity.Error);
                    Logs.PayServer.LogError(ex,
                        $"Ghost error while trying to create member on Ghost platform. {ex.Message}" +
                        $"Triggered by invoiceId: {invoice.Id}");
                }
            }
            ctx.UpdateRange(transaction);
            await ctx.SaveChangesAsync();
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
        }
    }
}

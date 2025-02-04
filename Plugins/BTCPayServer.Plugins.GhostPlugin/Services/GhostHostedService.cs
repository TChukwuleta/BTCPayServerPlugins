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

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase
{
    private readonly GhostPluginService _ghostPluginService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;

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
        Console.WriteLine($"Event type: {evt}");
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
                    var ghostOrderId = invoice.GetInternalTags(GhostApp.GHOST_MEMBER_ID_PREFIX).FirstOrDefault();
                    if (ghostOrderId != null)
                    {
                        string invoiceStatus = invoice.Status.ToString().ToLower();
                        bool? success = invoiceStatus switch
                        {
                            _ when new[] { "complete", "confirmed", "paid", "settled" }.Contains(invoiceStatus) => true,
                            _ when new[] { "invalid", "expired" }.Contains(invoiceStatus) => false,
                            _ => (bool?)null
                        };
                        if (success.HasValue)
                            await RegisterTransaction(invoice, ghostOrderId, success.Value);
                    }
                    break;
                }

            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged } paymentRequestStatusUpdated:
                {
                    if (paymentRequestStatusUpdated.Data.Status == Client.Models.PaymentRequestData.PaymentRequestStatus.Completed)
                    {
                        var prBlob = paymentRequestStatusUpdated.Data.GetBlob();
                        prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestSourceKey, out var src);
                        prBlob.AdditionalData.TryGetValue(GhostApp.MemberIdKey, out var memberIdToken);
                        if (src == null || src.Value<string>() != GhostApp.AppName || memberIdToken == null)
                            return;

                        if (paymentRequestStatusUpdated.Data.Status == Client.Models.PaymentRequestData.PaymentRequestStatus.Completed)
                        {
                            var memberId = memberIdToken?.Value<string>();
                            var blob = paymentRequestStatusUpdated.Data.GetBlob();
                            var memberEmail = blob.Email;
                            await _ghostPluginService.HandlePaidMembershipSubscription(prBlob, memberId, paymentRequestStatusUpdated.Data.Id, memberEmail);
                        }
                    }
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        Console.WriteLine(orderId);
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId);

        if (ghostSetting.CredentialsPopulated())
        {
            var result = new InvoiceLogs();

            result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
            var transaction = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id && c.TransactionStatus == TransactionStatus.Pending);
            if (transaction == null)
            {
                result.Write("Couldn't find a corresponding Ghost transaction table record", InvoiceEventData.EventSeverity.Error);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
                return;
            }
            transaction.InvoiceStatus = invoice.Status.ToString().ToLower();
            transaction.TransactionStatus = success ? TransactionStatus.Success : TransactionStatus.Failed;
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
            ctx.SaveChanges();
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
        }
    }
}

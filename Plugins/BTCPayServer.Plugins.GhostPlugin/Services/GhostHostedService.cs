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

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;

    public GhostHostedService(EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory httpClientFactory,
        GhostDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClientFactory = httpClientFactory;
    }

    private const string GHOST_MEMBER_ID_PREFIX = "Ghost_member-";

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is InvoiceEvent invoiceEvent && new[]
            {
                    InvoiceEvent.MarkedCompleted,
                    InvoiceEvent.MarkedInvalid,
                    InvoiceEvent.Expired,
                    InvoiceEvent.Confirmed,
                    InvoiceEvent.Completed
                }.Contains(invoiceEvent.Name))
        {
            var invoice = invoiceEvent.Invoice;
            var ghostOrderId = invoice.GetInternalTags(GHOST_MEMBER_ID_PREFIX).FirstOrDefault();
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
        }
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task RegisterTransaction(InvoiceEntity invoice, string shopifyOrderId, bool success)
    {
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

            var status = success ? "success" : "failure";
            transaction.InvoiceStatus = invoice.Status.ToString().ToLower();
            transaction.TransactionStatus = success ? TransactionStatus.Success : TransactionStatus.Failed;
            transaction.UpdatedAt = DateTime.UtcNow;
            ctx.UpdateRange(transaction);

            if (status == "success")
            {
                try
                {
                    var ghostMember = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == transaction.MemberId);
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
                                        expiry_at = ghostMember.Frequency == TierSubscriptionFrequency.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1)
                                    }
                                }
                            }
                        }
                    });
                    ghostMember.MemberId = response.members[0].id;
                    ghostMember.MemberUuid = response.members[0].uuid;
                    ghostMember.UnsubscribeUrl = response.members[0].unsubscribe_url;
                    ghostMember.MemberId = response.members[0].id;
                    ghostMember.SubscriptionId = response.members[0].subscriptions.First().id;
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
            ctx.SaveChanges();
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
        }
    }
}

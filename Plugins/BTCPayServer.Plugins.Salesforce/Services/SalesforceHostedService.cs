using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.Salesforce.Helper;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class SalesforceHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly SalesforceApiClient _salesforceApiClient;
    private readonly SalesforceDbContextFactory _dbContextFactory;

    public SalesforceHostedService(EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        SalesforceApiClient salesforceApiClient,
        SalesforceDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _salesforceApiClient = salesforceApiClient;
    }

    public const string SALESFORCE_ORDER_ID_PREFIX = "salesforce-";
    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is InvoiceEvent
            {
                Name:
                InvoiceEvent.MarkedCompleted or
                InvoiceEvent.MarkedInvalid or
                InvoiceEvent.Expired or
                InvoiceEvent.Confirmed or
                InvoiceEvent.FailedToConfirm,
                Invoice:
                {
                    Status:
                    InvoiceStatus.Settled or
                    InvoiceStatus.Invalid or
                    InvoiceStatus.Processing or
                    InvoiceStatus.Expired
                } invoice
            } && invoice.GetSalesforceOrderId() is { } salesforceOrderId)
        {
            try
            {
                var resp = await Process(salesforceOrderId, invoice);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, resp);
            }
            catch (Exception ex)
            {
                Logs.PayServer.LogError(ex,
                    $"Salesforce error while trying to register order status. " +
                    $"Triggered by invoiceId: {invoice.Id}, Shopify orderId: {salesforceOrderId}");
            }
        }

    }

    async Task<InvoiceLogs> Process(long shopifyOrderId, InvoiceEntity invoice)
    {
        var logs = new InvoiceLogs();
        await using var ctx = _dbContextFactory.CreateContext();
        var salesforceSettings = ctx.SalesforceSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId);
        if (salesforceSettings == null) 
            return logs;

        try
        {
            await _salesforceApiClient.WebhookNotification(salesforceSettings, invoice.Id, invoice.Status.ToString(), invoice.StoreId, "12");
            logs.Write(
                $"Successfully captured the order on Salesforce. ({invoice.Currency})",
                InvoiceEventData.EventSeverity.Info);
        }
        catch (Exception e)
        {
            logs.Write($"Failed to capture the Salesforce order. ({invoice.Currency}) {e.Message} ",
                InvoiceEventData.EventSeverity.Error);
        }
        return logs;
    }
}

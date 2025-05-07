using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.Salesforce.Helper;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class SalesforceHostedService : EventHostedServiceBase
{
    private readonly CurrencyNameTable _currencyNameTable;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly SalesforceApiClient _salesforceApiClient;
    private readonly SalesforceDbContextFactory _dbContextFactory;

    public SalesforceHostedService(EventAggregator eventAggregator,
        InvoiceRepository invoiceRepository,
        CurrencyNameTable currencyNameTable,
        SalesforceApiClient salesforceApiClient,
        SalesforceDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _dbContextFactory = dbContextFactory;
        _currencyNameTable = currencyNameTable;
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
                    InvoiceStatus.Expired
                } invoice
            } && invoice.GetSalesforceOrderId() is { } shopifyOrderId)
        {
            try
            {
                var resp = await Process(shopifyOrderId, invoice);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, resp);
            }
            catch (Exception ex)
            {
                Logs.PayServer.LogError(ex,
                    $"Shopify error while trying to register order transaction. " +
                    $"Triggered by invoiceId: {invoice.Id}, Shopify orderId: {shopifyOrderId}");
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

        var salesforceOrderId = invoice.GetSalesforceOrderId();
        // An api call to verify orderId from salesforce
        decimal? btcpayPaid = invoice switch
        {
            { Status: InvoiceStatus.Settled } => invoice.Price,
            { Status: InvoiceStatus.Expired, ExceptionStatus: InvoiceExceptionStatus.PaidPartial } => NetSettled(invoice),
            { Status: InvoiceStatus.Invalid, ExceptionStatus: InvoiceExceptionStatus.Marked } => 0.0m,
            { Status: InvoiceStatus.Invalid } => NetSettled(invoice),
            _ => null
        };
        try
        {
            await _salesforceApiClient.UpdateSalesforceOrder(salesforceSettings, salesforceOrderId.ToString(), invoice.Status.ToString());
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


    private decimal NetSettled(InvoiceEntity invoice)
    {
        decimal netSettled = netSettled = invoice.GetPayments(true)
                        .Where(payment => payment.Status == PaymentStatus.Settled)
                        .Sum(payment => payment.InvoicePaidAmount.Net);
        // Later we can just use this instead of calculating ourselves
        // decimal netSettled = invoice.NetSettled;
        return Math.Round(netSettled, _currencyNameTable.GetNumberFormatInfo(invoice.Currency)?.CurrencyDecimalDigits ?? 2);
    }
}

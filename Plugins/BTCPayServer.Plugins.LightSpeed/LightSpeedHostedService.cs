using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using Microsoft.Extensions.Logging;
using NBitpayClient;

namespace BTCPayServer.Plugins.LightSpeed;

internal class LightSpeedHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly CurrencyNameTable _currencyNameTable;
    public LightSpeedHostedService(EventAggregator eventAggregator,InvoiceRepository invoiceRepository, Logs logs, 
        CurrencyNameTable currencyNameTable) : base(eventAggregator, logs)
    {
        _invoiceRepository = invoiceRepository;
        _currencyNameTable = currencyNameTable;
    }


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
            }) // && invoice.GetSaleorOrderId() is { } saleorTransactionId)
        {
            try
            {
                var resp = await Process(saleorTransactionId, invoice);
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, resp);
            }
            catch (Exception ex)
            {
                Logs.PayServer.LogError(ex,
                    $"Saleor error while trying to register order transaction. " +
                    $"Triggered by invoiceId: {invoice.Id}, Saleor transactionId: {saleorTransactionId}");
            }
        }
    }



    async Task<InvoiceLogs> Process(string saleorTransactionId, InvoiceEntity invoice)
    {
        var logs = new InvoiceLogs();
        string result = invoice switch
        {
            { Status: InvoiceStatus.Settled } => "CHARGE_SUCCESS",
            { Status: InvoiceStatus.Expired } => "CHARGE_FAILURE",
            { Status: InvoiceStatus.Invalid } => "CHARGE_FAILURE",
            _ => "CHARGE_ACTION_REQUIRED"
        };
        logs.Write($"Saleor transaction Id is: {saleorTransactionId}", InvoiceEventData.EventSeverity.Warning);


        var paymentStatus = newStatus switch
        {
            InvoiceStatus.Settled => PaymentStatus.Settled,
            InvoiceStatus.Expired => PaymentStatus.Expired,
            InvoiceStatus.Invalid => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };

        await _settings.UpdatePaymentStatusAsync(invoiceId, paymentStatus);

        _logger.LogInformation(
            "Invoice {InvoiceId} transitioned to {Status}",
            invoiceId, paymentStatus);


        try
        {
            var externalUrl = $"{invoice.ServerUrl}i/{invoice.Id}/receipt";
            var amountPaid = Math.Round(invoice.PaidAmount.Net, _currencyNameTable.GetNumberFormatInfo(invoice.Currency)?.CurrencyDecimalDigits ?? 2);
            logs.Write($"Reporting to Saleor: transactionId={saleorTransactionId}, amount={amountPaid}, type={result}, pspRef={invoice.Id}",
             InvoiceEventData.EventSeverity.Info);
            logs.Write($"Reported {result} to Saleor for transaction {saleorTransactionId}", InvoiceEventData.EventSeverity.Info);
        }
        catch (Exception ex)
        {
            logs.Write($"Saleor error while trying to write status for transaction {saleorTransactionId}. {ex.Message}",
                InvoiceEventData.EventSeverity.Error);
        }
        return logs;
    }
}


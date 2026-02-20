using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.Saleor.Services;
using BTCPayServer.Services.Invoices;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor;

public class SaleorHostedService : EventHostedServiceBase
{
    private readonly SaleorGraphQLService _graphql;
    private readonly InvoiceRepository _invoiceRepository;
    public SaleorHostedService(EventAggregator eventAggregator, 
        SaleorGraphQLService graphql,
        InvoiceRepository invoiceRepository, Logs logs) : base(eventAggregator, logs)
    {
        _graphql = graphql;
        _invoiceRepository = invoiceRepository;
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
            } && invoice.GetSaleorOrderId() is { } saleorTransactionId)
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


    async Task<InvoiceLogs> Process(string saleorOrderId, InvoiceEntity invoice)
    {
        var logs = new InvoiceLogs();

        string result = invoice switch
        {
            { Status: InvoiceStatus.Settled } => "CHARGE_SUCCESS",
            { Status: InvoiceStatus.Expired } => "CHARGE_FAILURE",
            { Status: InvoiceStatus.Invalid } => "CHARGE_FAILURE",
            _ => "CHARGE_ACTION_REQUIRED"
        };


        /*if (btcpayPaid is not null)
        {
            var capture = btcpayPaid.Value - shopifyPaid;
            if (capture > 0m)
            {
                if (order.CancelledAt is not null)
                {
                    logs.Write("The shopify order has already been cancelled, but the BTCPay Server has been successfully paid.",
                        InvoiceEventData.EventSeverity.Warning);
                    return logs;
                }

                if (saleTx.ManuallyCapturable)
                {
                    try
                    {
                        await client.CaptureOrder(new()
                        {
                            Currency = invoice.Currency,
                            Amount = capture,
                            Id = order.Id,
                            ParentTransactionId = saleTx.Id
                        });
                        logs.Write(
                            $"Successfully captured the order on Shopify. ({capture} {invoice.Currency})",
                            InvoiceEventData.EventSeverity.Info);
                    }
                    catch (Exception e)
                    {
                        logs.Write($"Failed to capture the Shopify order. ({capture} {invoice.Currency}) {e.Message} ",
                            InvoiceEventData.EventSeverity.Error);
                    }
                }
            }
        }
        else if (order.CancelledAt is null)
        {
            try
            {
                await client.CancelOrder(new()
                {
                    OrderId = order.Id,
                    NotifyCustomer = false,
                    Reason = OrderCancelReason.DECLINED,
                    Restock = true,
                    Refund = false,
                    StaffNote = $"BTCPay Invoice {invoice.Id} is {invoice.Status}"
                });
                logs.Write($"Shopify order cancelled. (Invoice Status: {invoice.Status})", InvoiceEventData.EventSeverity.Warning);
            }
            catch (Exception e)
            {
                logs.Write($"Failed to cancel the Shopify order. {e.Message}",
                    InvoiceEventData.EventSeverity.Error);
            }
        }*/
        return logs;
    }


}

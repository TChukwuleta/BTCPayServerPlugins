using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.PaymentRequests;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor;

public class SaleorHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    public SaleorHostedService(EventAggregator eventAggregator, InvoiceRepository invoiceRepository, Logs logs) : base(eventAggregator, logs)
    {
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
            } && invoice.GetSaleorOrderId() is { } saleorOrderId)
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


}

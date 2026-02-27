using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.JumpSeller.Services;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.JumpSeller;

internal class JumpSellerHostedService : EventHostedServiceBase
{
    private readonly JumpSellerService _jumpSellerService;
    public JumpSellerHostedService(EventAggregator eventAggregator, Logs logs, JumpSellerService jumpSellerService) : base(eventAggregator, logs)
    {
        _jumpSellerService = jumpSellerService;
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
            })
        {
            try
            {
                var invoiceData = await _jumpSellerService.GetInvoiceData(invoice.Id);
                if (invoiceData == null) return;

                var settings = await _jumpSellerService.GetSettings(invoiceData.StoreId);
                if (settings is null) return;

                var result = _jumpSellerService.MapInvoiceStatusToResult(invoice, settings, out var message);
                await _jumpSellerService.SendCallback(invoiceData, settings, result, message);
            }
            catch (Exception ex)
            {
                Logs.PayServer.LogError(ex, $"Jumpseller error while trying to register order transaction for light speed");
            }
        }
    }
}
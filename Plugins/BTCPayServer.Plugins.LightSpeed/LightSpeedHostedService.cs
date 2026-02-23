using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.LightSpeed.Data;
using BTCPayServer.Plugins.LightSpeed.Services;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.LightSpeed;

internal class LightSpeedHostedService : EventHostedServiceBase
{
    private readonly LightSpeedService _lightSpeedService;
    public LightSpeedHostedService(EventAggregator eventAggregator, Logs logs, LightSpeedService lightSpeedService) : base(eventAggregator, logs)
    {
        _lightSpeedService = lightSpeedService;
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
                LightSpeedPaymentStatus? result = invoice switch
                {
                    { Status: InvoiceStatus.Settled } => LightSpeedPaymentStatus.Settled,
                    { Status: InvoiceStatus.Expired } => LightSpeedPaymentStatus.Expired,
                    { Status: InvoiceStatus.Invalid } => LightSpeedPaymentStatus.Failed,
                    _ => null
                };
                if (result is not null)
                {
                    await _lightSpeedService.UpdatePaymentStatus(invoice.Id, result.Value);
                }
            }
            catch (Exception ex)
            {
                Logs.PayServer.LogError(ex, $"Saleor error while trying to register order transaction for light speed");
            }
        }
    }
}
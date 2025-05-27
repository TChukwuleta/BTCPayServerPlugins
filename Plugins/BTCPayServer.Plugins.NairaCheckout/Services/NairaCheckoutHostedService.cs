using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.PaymentRequests;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class NairaCheckoutHostedService : EventHostedServiceBase
{
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;

    public NairaCheckoutHostedService(EventAggregator eventAggregator,
        NairaCheckoutDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _dbContextFactory = dbContextFactory;
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
                    var ghostOrderId = invoice.GetInternalTags("GHOST_PREFIX").FirstOrDefault();
                    if (ghostOrderId != null)
                    {
                        bool? success = invoice.Status switch
                        {
                            InvoiceStatus.Settled => true,
                            InvoiceStatus.Invalid or
                            InvoiceStatus.Expired => false,
                            _ => (bool?)null
                        };
                        if (success.HasValue && ghostOrderId.StartsWith("GHOST_MEMBER_ID_PREFIX"))
                        {
                            await RegisterMembershipCreationTransaction(invoice, ghostOrderId, success.Value);
                        }
                    }
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
        return;
    }
}

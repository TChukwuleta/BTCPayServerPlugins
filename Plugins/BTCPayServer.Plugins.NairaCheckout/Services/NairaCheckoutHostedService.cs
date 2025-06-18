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
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class NairaCheckoutHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;

    public NairaCheckoutHostedService(EventAggregator eventAggregator,
        NairaCheckoutDbContextFactory dbContextFactory,
        InvoiceRepository invoiceRepository,
        Logs logs) : base(eventAggregator, logs)
    {
        _invoiceRepository = invoiceRepository;
        _dbContextFactory = dbContextFactory;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
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
                    bool? success = invoice.Status switch
                    {
                        InvoiceStatus.Settled => true,
                        InvoiceStatus.Invalid or
                        InvoiceStatus.Expired => false,
                        _ => (bool?)null
                    };
                    if (success.HasValue)
                    {
                        await RegisterTransactionOrder(invoice, success.Value);
                    }
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterTransactionOrder(InvoiceEntity invoice, bool success)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var order = ctx.NairaCheckoutOrders.FirstOrDefault(c => c.InvoiceId == invoice.Id && c.StoreId == invoice.StoreId);
        if (order == null) return;

        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        result.Write($"Writing Naira checkout transaction payment", InvoiceEventData.EventSeverity.Info);
        try
        {
            order.ThirdPartyStatus = success ? invoice.Status.ToString() : invoice.ExceptionStatus.ToString();
            order.ThirdPartyMarkedPaid = success;
            order.UpdatedAt = DateTime.UtcNow;
            ctx.NairaCheckoutOrders.Update(order);
            await ctx.SaveChangesAsync();
            result.Write($"Successfully recored naira checkout.", InvoiceEventData.EventSeverity.Info);
        }
        catch (Exception ex)
        {
            Logs.PayServer.LogError(ex,
                $"Naira plugin error while trying to save. {ex.Message}" +
                $"Triggered by invoiceId: {invoice.Id}");
        }
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }
}

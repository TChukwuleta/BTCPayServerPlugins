using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Data;
using BTCPayServer.Services.Invoices;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceInvoicesPaidHostedService : EventHostedServiceBase
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly BigCommerceDbContextFactory _contextFactory;
    private readonly BigCommerceService _bigCommerceService;

    public BigCommerceInvoicesPaidHostedService(
        InvoiceRepository invoiceRepository,
        BigCommerceService bigCommerceService,
        BigCommerceDbContextFactory contextFactory, 
        EventAggregator eventAggregator, Logs logs) : base(eventAggregator, logs)
    {
        _contextFactory = contextFactory;
        _invoiceRepository = invoiceRepository;
        _bigCommerceService = bigCommerceService;
    }
    public const string BIGCOMMERCE_ORDER_ID_PREFIX = "BigCommerce-";


    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }


    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is InvoiceEvent invoiceEvent && !new[]
        {
                InvoiceEvent.MarkedCompleted,
                InvoiceEvent.MarkedInvalid,
                InvoiceEvent.Expired,
                InvoiceEvent.Confirmed,
                InvoiceEvent.Completed
            }.Contains(invoiceEvent.Name))
        {
            var invoice = invoiceEvent.Invoice;
            await using var ctx = _contextFactory.CreateContext();
            var bigCommerceStoreTransaction = ctx.Transactions.FirstOrDefault(c => c.InvoiceId == invoice.Id && c.TransactionStatus == Data.TransactionStatus.Pending);
            if (bigCommerceStoreTransaction != null)
            {
                var result = new InvoiceLogs();
                if (new[] { "complete", "confirmed", "paid", "settled" }.Contains(invoice.Status.ToString().ToLower()) ||
                    (invoice.Status.ToString().ToLower() == "expired" && 
                     (invoice.ExceptionStatus is InvoiceExceptionStatus.PaidLate or InvoiceExceptionStatus.PaidOver)))
                {
                    var bigCommerceStore = ctx.BigCommerceStores.AsNoTracking().FirstOrDefault(c => c.StoreId == bigCommerceStoreTransaction.StoreId);
                    string orderNumberId = bigCommerceStoreTransaction.OrderId.Substring(BIGCOMMERCE_ORDER_ID_PREFIX.Length);
                    int.TryParse(orderNumberId, out int orderId);

                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Success;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                    bool confirmOrder = await _bigCommerceService.ConfirmOrderExistAsync(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                    if (confirmOrder)
                    {
                        result.Write("Order successfully confirmed on big commerce", InvoiceEventData.EventSeverity.Success);
                        await _bigCommerceService.UpdateOrderStatusAsync(orderId, Data.BigCommerceOrderState.COMPLETED, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                        result.Write("Order status successfully updated on big commerce", InvoiceEventData.EventSeverity.Success);
                    }
                    else
                    {
                        result.Write("Couldn't find the order on BigCommerce.", InvoiceEventData.EventSeverity.Error);
                    }
                }
                else if (new[] { "invalid", "expired" }.Contains(invoice.GetInvoiceState()
                    .Status.ToString().ToLower()) && invoice.ExceptionStatus != InvoiceExceptionStatus.None)
                {
                    result.Write($"Invoice payment failed. Invoice status: {invoice.GetInvoiceState()
                    .Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Error);
                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Failed;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                }
                await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);

                ctx.Update(bigCommerceStoreTransaction);
                await ctx.SaveChangesAsync();
            }
        }

        await base.ProcessEvent(evt, cancellationToken);
    }
}

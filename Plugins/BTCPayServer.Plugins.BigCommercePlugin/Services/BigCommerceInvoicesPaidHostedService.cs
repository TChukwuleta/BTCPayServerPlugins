using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Client.Models;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceInvoicesPaidHostedService : EventHostedServiceBase
{
    private readonly BigCommerceDbContextFactory _contextFactory;
    private readonly BigCommerceService _bigCommerceService;

    public BigCommerceInvoicesPaidHostedService(
        BigCommerceService bigCommerceService,
        BigCommerceDbContextFactory contextFactory, 
        EventAggregator eventAggregator, Logs logs) : base(eventAggregator, logs)
    {
        _contextFactory = contextFactory;
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
                InvoiceEvent.Created, InvoiceEvent.ExpiredPaidPartial,
                InvoiceEvent.ReceivedPayment, InvoiceEvent.PaidInFull
            }.Contains(invoiceEvent.Name))
        {
            var invoice = invoiceEvent.Invoice;
            var bigCommerceOrderId = invoice.GetInternalTags(BIGCOMMERCE_ORDER_ID_PREFIX).FirstOrDefault();
            if (bigCommerceOrderId != null)
            {
                await using var ctx = _contextFactory.CreateContext();

                var bigCommerceStoreTransaction = ctx.Transactions.FirstOrDefault(c => c.OrderId == bigCommerceOrderId);
                if (new[] { InvoiceStatusLegacy.Invalid, InvoiceStatusLegacy.Expired }.Contains(invoice.GetInvoiceState()
                    .Status) && invoice.ExceptionStatus != InvoiceExceptionStatus.None)
                {
                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Failed;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                }
                else if (new[] { InvoiceStatusLegacy.Complete, InvoiceStatusLegacy.Confirmed }.Contains(
                    invoice.Status))
                {
                    var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == bigCommerceStoreTransaction.StoreId);
                    string orderNumberId = bigCommerceOrderId.Substring(BIGCOMMERCE_ORDER_ID_PREFIX.Length);
                    int.TryParse(orderNumberId, out int orderId);

                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Success;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                    bool confirmOrder = await _bigCommerceService.ConfirmOrderExistAsync(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                    if (confirmOrder)
                    {
                        await _bigCommerceService.UpdateOrderStatusAsync(orderId, Data.BigCommerceOrderState.COMPLETED, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                    }
                }
                ctx.Update(bigCommerceStoreTransaction);
                await ctx.SaveChangesAsync();
            }
        }

        await base.ProcessEvent(evt, cancellationToken);
    }
}

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
    private BTCPayNetworkProvider NetworkProvider { get; }

    public BigCommerceInvoicesPaidHostedService(
        BigCommerceService bigCommerceService,
        BigCommerceDbContextFactory contextFactory, 
        BTCPayNetworkProvider networkProvider, 
        EventAggregator eventAggregator, Logs logs) : base(eventAggregator, logs)
    {
        NetworkProvider = networkProvider;
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
                    //you have failed us, customer

                }
                else if (new[] { InvoiceStatusLegacy.Complete, InvoiceStatusLegacy.Confirmed }.Contains(
                    invoice.Status))
                {
                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Failed;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;

                    // Call Big commerce to confirm


                    /*var client = CreateShopifyApiClient(settings);
                    if (!await client.OrderExists(shopifyOrderId))
                    {
                        // don't register transactions for orders that don't exist on shopify
                        return;
                    }*/

                    // if we got this far, we likely need to register this invoice's payment on Shopify
                    // OrderTransactionRegisterLogic has check if transaction is already registered which is why we're passing invoice.Id
                }

                ctx.Update(bigCommerceStoreTransaction);
                await ctx.SaveChangesAsync();
            }
        }

        await base.ProcessEvent(evt, cancellationToken);
    }
}

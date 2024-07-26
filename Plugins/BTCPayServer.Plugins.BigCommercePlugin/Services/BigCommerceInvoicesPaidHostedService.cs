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

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceInvoicesPaidHostedService : EventHostedServiceBase
{
    private readonly BigCommerceDbContextFactory _contextFactory;
    private readonly ILogger<BigCommerceInvoicesPaidHostedService> _logger;
    private readonly BigCommerceService _bigCommerceService;

    public BigCommerceInvoicesPaidHostedService(
        BigCommerceService bigCommerceService,
        BigCommerceDbContextFactory contextFactory, 
        EventAggregator eventAggregator, Logs logs, ILogger<BigCommerceInvoicesPaidHostedService> logger) : base(eventAggregator, logs)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _bigCommerceService = bigCommerceService;
    }
    public const string BIGCOMMERCE_ORDER_ID_PREFIX = "BigCommerce-";


    protected override void SubscribeToEvents()
    {
        _logger.LogInformation("Subscribe to Event");
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }


    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"About to process events");
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
            _logger.LogInformation($"Gotten hereeee. Invoice Id: {invoice.Id}...Status: {invoice.Status.ToString()}");
            await using var ctx = _contextFactory.CreateContext();
            var bigCommerceStoreTransaction = ctx.Transactions.FirstOrDefault(c => c.InvoiceId == invoice.Id && c.TransactionStatus == Data.TransactionStatus.Pending);
            if (bigCommerceStoreTransaction != null)
            {
                _logger.LogInformation($"Full invoice entity detail: {JsonConvert.SerializeObject(invoice)}");
                if (new[] { "complete", "confirmed", "paid", "settled" }.Contains(invoice.Status.ToString().ToLower()) ||
                    (invoice.Status.ToString().ToLower() == "expired" && 
                     (invoice.ExceptionStatus is InvoiceExceptionStatus.PaidLate or InvoiceExceptionStatus.PaidOver)))
                {

                    var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == bigCommerceStoreTransaction.StoreId);
                    string orderNumberId = bigCommerceStoreTransaction.OrderId.Substring(BIGCOMMERCE_ORDER_ID_PREFIX.Length);
                    int.TryParse(orderNumberId, out int orderId);

                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Success;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                    try
                    {
                        _logger.LogInformation("About to confirm order");
                        bool confirmOrder = await _bigCommerceService.ConfirmOrderExistAsync(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                        if (confirmOrder)
                        {
                            _logger.LogInformation($"Done confirming order");
                            await _bigCommerceService.UpdateOrderStatusAsync(orderId, Data.BigCommerceOrderState.COMPLETED, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                        }
                        _logger.LogInformation("Done Done");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"An error occured: {ex.Message}");
                        throw;
                    }
                }
                else if (new[] { "invalid", "expired" }.Contains(invoice.GetInvoiceState()
                    .Status.ToString().ToLower()) && invoice.ExceptionStatus != InvoiceExceptionStatus.None)
                {
                    bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Failed;
                    bigCommerceStoreTransaction.InvoiceId = invoice.Id;
                }

                ctx.Update(bigCommerceStoreTransaction);
                await ctx.SaveChangesAsync();
            }
        }

        await base.ProcessEvent(evt, cancellationToken);
    }
}

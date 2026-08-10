using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;

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
        if (evt is InvoiceEvent invoiceEvent && new[]
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
                if (IsSuccessfulInvoice(invoice))
                {
                    await HandleSuccessfulInvoice(ctx, invoice, bigCommerceStoreTransaction, result);
                }
                else if (IsFailedInvoice(invoice))
                {
                    result.Write($"Invoice payment failed. Invoice status: {invoice.GetInvoiceState().Status}", InvoiceEventData.EventSeverity.Error);
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

    private async Task HandleSuccessfulInvoice(BigCommerceDbContext ctx, InvoiceEntity invoice, Transaction bigCommerceStoreTransaction, InvoiceLogs result)
    {
        var bigCommerceStore = await ctx.BigCommerceStores.FirstOrDefaultAsync(c => c.StoreId == bigCommerceStoreTransaction.StoreId);
        if (bigCommerceStore == null)
        {
            result.Write("BigCommerce store not found.", InvoiceEventData.EventSeverity.Error);
            return;
        }
        string orderNumberId = bigCommerceStoreTransaction.OrderId.Substring(BIGCOMMERCE_ORDER_ID_PREFIX.Length);
        if (!long.TryParse(orderNumberId, out long orderId))
        {
            result.Write("Invalid order number format.", InvoiceEventData.EventSeverity.Error);
            return;
        }

        var orderDetails = await _bigCommerceService.GetOrder(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
        if (orderDetails == null)
        {
            result.Write("Couldn't find the order on BigCommerce.", InvoiceEventData.EventSeverity.Error);
            return;
        }
        if (!decimal.TryParse(orderDetails.total_inc_tax, NumberStyles.Any, CultureInfo.InvariantCulture, out var authoritativeTotal) ||
            !string.Equals(orderDetails.currency_code, invoice.Currency, StringComparison.OrdinalIgnoreCase) || authoritativeTotal > invoice.Price)
        {
            result.Write($"Refusing to mark order {orderId} as fulfillable: settled invoice ({invoice.Price} {invoice.Currency}) does not cover BigCommerce's order total ({orderDetails.total_inc_tax} {orderDetails.currency_code}).", InvoiceEventData.EventSeverity.Error);
            return;
        }

        bigCommerceStoreTransaction.InvoiceId = invoice.Id;
        bool confirmOrder = await _bigCommerceService.ConfirmOrderExist(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
        if (!confirmOrder)
        {
            result.Write("Couldn't find the order on BigCommerce.", InvoiceEventData.EventSeverity.Error);
            return;
        }

        result.Write("Order successfully confirmed on BigCommerce.", InvoiceEventData.EventSeverity.Success);
        bool updated = await _bigCommerceService.UpdateOrderStatus(orderId, BigCommerceOrderState.AWAITING_FULFILLMENT, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
        bigCommerceStoreTransaction.InvoiceId = invoice.Id;
        if (!updated)
        {
            result.Write("Order confirmed but the status update to BigCommerce failed. Order is NOT yet marked fulfillable.", InvoiceEventData.EventSeverity.Error);
            return;
        }
        bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Success;
        result.Write("Order successfully confirmed and updated on BigCommerce.", InvoiceEventData.EventSeverity.Success);
    }

    private bool IsSuccessfulInvoice(InvoiceEntity invoice)
    {
        var isSuccessfulStatus = invoice.Status is InvoiceStatus.Settled;
        var isPaidLateOrOver = invoice.ExceptionStatus is InvoiceExceptionStatus.PaidLate or InvoiceExceptionStatus.PaidOver;
        return isSuccessfulStatus || (invoice.Status == InvoiceStatus.Expired && isPaidLateOrOver);
    }

    private bool IsFailedInvoice(InvoiceEntity invoice)
    {
        var status = invoice.GetInvoiceState().Status;
        var isFailedStatus = status is InvoiceStatus.Invalid or InvoiceStatus.Expired;
        var hasException = invoice.ExceptionStatus != InvoiceExceptionStatus.None;
        return isFailedStatus && hasException;
    }
}

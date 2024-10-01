using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Data;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Plugins.BigCommercePlugin.Data;

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

            var bigCommerceStoreTransaction = await ctx.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(c => c.InvoiceId == invoice.Id && c.TransactionStatus == Data.TransactionStatus.Pending);

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
        var bigCommerceStore = await ctx.BigCommerceStores.AsNoTracking().FirstOrDefaultAsync(c => c.StoreId == bigCommerceStoreTransaction.StoreId);
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

        bigCommerceStoreTransaction.TransactionStatus = Data.TransactionStatus.Success;
        bigCommerceStoreTransaction.InvoiceId = invoice.Id;
        bool confirmOrder = await _bigCommerceService.ConfirmOrderExistAsync(orderId, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
        if (confirmOrder)
        {
            result.Write("Order successfully confirmed on BigCommerce.", InvoiceEventData.EventSeverity.Success);
            await _bigCommerceService.UpdateOrderStatusAsync(orderId, BigCommerceOrderState.COMPLETED, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
            result.Write("Order status successfully updated on BigCommerce.", InvoiceEventData.EventSeverity.Success);
        }
        else
        {
            result.Write("Couldn't find the order on BigCommerce.", InvoiceEventData.EventSeverity.Error);
        }
    }

    private bool IsSuccessfulInvoice(InvoiceEntity invoice)
    {
        var successfulStatuses = new[] { "complete", "confirmed", "paid", "settled" };
        var invoiceStatus = invoice.Status.ToString();
        var isPaidLateOrOver = invoice.ExceptionStatus is InvoiceExceptionStatus.PaidLate or InvoiceExceptionStatus.PaidOver;
        return successfulStatuses.Contains(invoiceStatus, StringComparer.OrdinalIgnoreCase) ||
               (invoiceStatus.Equals("expired", StringComparison.OrdinalIgnoreCase) && isPaidLateOrOver);
    }

    private bool IsFailedInvoice(InvoiceEntity invoice)
    {
        var failedStatuses = new[] { "invalid", "expired" };
        return failedStatuses.Contains(invoice.GetInvoiceState().Status.ToString(), StringComparer.OrdinalIgnoreCase) &&
               invoice.ExceptionStatus != InvoiceExceptionStatus.None;
    }
}

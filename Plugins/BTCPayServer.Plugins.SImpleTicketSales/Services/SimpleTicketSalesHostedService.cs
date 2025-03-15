using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Services.Mails;
using BTCPayServer.Client.Models;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SimpleTicketSales.Services;

public class SimpleTicketSalesHostedService : EventHostedServiceBase
{
    public const string TICKET_SALES_PREFIX = "Ticket_Sales_";
    private readonly EmailService _emailService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;

    public SimpleTicketSalesHostedService(EmailService emailService,
        EventAggregator eventAggregator,
        EmailSenderFactory emailSenderFactory,
        InvoiceRepository invoiceRepository,
        SimpleTicketSalesDbContextFactory dbContextFactory,
        Logs logs) : base(eventAggregator, logs)
    {
        _emailService = emailService;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _emailSenderFactory = emailSenderFactory;
    }

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
                var ticketOrderId = invoice.GetInternalTags(TICKET_SALES_PREFIX).FirstOrDefault();
                if (ticketOrderId != null)
                {
                    bool? success = invoice.Status switch
                    {
                        InvoiceStatus.Settled => true,
                        InvoiceStatus.Invalid or InvoiceStatus.Expired => false,
                        _ => null
                    };
                    if (success.HasValue)
                    {
                        await RegisterTicketTransaction(invoice, ticketOrderId, success.Value);
                    }
                }
            }
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task RegisterTicketTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets).FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id);
        if (order == null) return;

        if (order != null && order.PaymentStatus != Data.TransactionStatus.New.ToString())
        {
            result.Write("Transaction has previously been completed", InvoiceEventData.EventSeverity.Info);
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
            return;
        }

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == order.EventId && c.StoreId == order.StoreId);
        order.PurchaseDate = DateTime.UtcNow;
        order.InvoiceStatus = invoice.Status.ToString().ToLower();
        order.PaymentStatus = success ? Data.TransactionStatus.Settled.ToString() : Data.TransactionStatus.Expired.ToString();
        order.Tickets.ToList().ForEach(c => c.PaymentStatus = success ? Data.TransactionStatus.Settled.ToString() : Data.TransactionStatus.Expired.ToString());
        result.Write($"New ticket payment completed for Event: {ticketEvent?.Title}, Order Id: {order.Id}, Order Txn Id: {order.TxnId}", InvoiceEventData.EventSeverity.Success);
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();

        var emailSender = await _emailSenderFactory.GetEmailSender(invoice.StoreId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (isEmailSettingsConfigured)
        {
            try
            {
                var emailResponse = await _emailService.SendTicketRegistrationEmail(invoice.StoreId, order.Tickets, ticketEvent);
                var failedRecipients = new HashSet<string>(emailResponse.FailedRecipients);
                foreach (var ticket in order.Tickets)
                {
                    ticket.EmailSent = !failedRecipients.Contains(ticket.Email);
                }
                result.Write($"Email sent successfully to recipients in Order with Id: {order.Id}", InvoiceEventData.EventSeverity.Success);
            }
            catch (Exception) { }
            ctx.Orders.Update(order);
            await ctx.SaveChangesAsync();
        }
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }
}

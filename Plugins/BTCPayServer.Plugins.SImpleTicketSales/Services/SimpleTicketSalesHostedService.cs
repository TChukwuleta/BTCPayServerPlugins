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
                    var ghostOrderId = invoice.GetInternalTags(TICKET_SALES_PREFIX).FirstOrDefault();
                    if (ghostOrderId != null)
                    {
                        bool? success = invoice.Status switch
                        {
                            InvoiceStatus.Settled => true,

                            InvoiceStatus.Invalid or
                            InvoiceStatus.Expired => false,

                            _ => (bool?)null
                        };
                        if (success.HasValue)
                        {
                            await RegisterTicketTransaction(invoice, ghostOrderId, success.Value);
                        }
                    }
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task RegisterTicketTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        var ticket = ctx.TicketSalesEventTickets.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id);
        if (ticket == null) return;

        if (ticket != null && ticket.PaymentStatus != Data.TransactionStatus.New.ToString())
        {
            result.Write("Transaction has previously been completed", InvoiceEventData.EventSeverity.Info);
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
            return;
        }
        var ghostEvent = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == ticket.EventId && c.StoreId == ticket.StoreId);
        ticket.PurchaseDate = DateTime.UtcNow;
        ticket.InvoiceStatus = invoice.Status.ToString().ToLower();
        ticket.PaymentStatus = success ? Data.TransactionStatus.Settled.ToString() : Data.TransactionStatus.Expired.ToString();
        result.Write($"New ticket payment completed for Event: {ghostEvent?.Title} Buyer name: {ticket.Name}", InvoiceEventData.EventSeverity.Success);

        var emailSender = await _emailSenderFactory.GetEmailSender(invoice.StoreId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (isEmailSettingsConfigured)
        {
            try
            {
                await _emailService.SendTicketRegistrationEmail(invoice.StoreId, ticket, ghostEvent);
                ticket.EmailSent = true;
                result.Write($"Email sent successfully to: {ticket?.Email}", InvoiceEventData.EventSeverity.Success);
            }
            catch (Exception) { }
        }
        ctx.UpdateRange(ticket);
        await ctx.SaveChangesAsync();
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public class SimpleTicketSalesHostedService : EventHostedServiceBase, IPeriodicTask
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
        SimpleTicketSalesDbContextFactory dbContextFactory, Logs logs) : base(eventAggregator, logs)
    {
        _emailService = emailService;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _emailSenderFactory = emailSenderFactory;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        Subscribe<PeriodProcessEvent>();
        base.SubscribeToEvents();
    }
    public class PeriodProcessEvent
    {
        public string StoreId { get; set; }
        public SatoshiTicketsSetting Setting { get; set; }
    }


    public async Task Do(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = _dbContextFactory.CreateContext();
            var storeSetings = db.SatoshiTicketsSettings.Where(s => s.EnableAutoReminders).ToList();
            foreach (var settings in storeSetings)
            {
                if (!await _emailService.IsEmailSettingsConfigured(settings.StoreId))
                    continue;

                PushEvent(new PeriodProcessEvent { StoreId = settings.StoreId, Setting = settings });
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            Logs.PayServer.LogInformation("Skipping task: SatoshiTickets table not created yet.");
        }
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is PeriodProcessEvent sequentialExecute)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var now = DateTimeOffset.UtcNow;

            var upcomingEvents = ctx.Events.Where(e => e.EventState == Data.EntityState.Active && e.StartDate > now.UtcDateTime
                         && e.ReminderSentAt == null && e.StoreId == sequentialExecute.StoreId).ToList();

            if (!upcomingEvents.Any()) return;

            foreach (var ticketEvent in upcomingEvents)
            {
                if (!ticketEvent.ReminderEnabled) continue;

                var settings = sequentialExecute.Setting;

                int effectiveDays = ticketEvent.ReminderDaysBeforeEvent ?? (settings?.DefaultReminderDaysBeforeEvent ?? 3);
                var daysUntilEvent = (ticketEvent.StartDate - now.UtcDateTime).TotalDays;
                if (daysUntilEvent > effectiveDays) continue;

                if (!await _emailService.IsEmailSettingsConfigured(ticketEvent.StoreId)) continue;

                var settledTickets = ctx.Orders.Include(o => o.Tickets).Where(o => o.EventId == ticketEvent.Id
                             && o.StoreId == ticketEvent.StoreId && o.PaymentStatus == Data.TransactionStatus.Settled.ToString())
                    .SelectMany(o => o.Tickets).ToList().DistinctBy(t => t.Email).ToList();

                if (!settledTickets.Any()) continue;

                try
                {
                    await _emailService.SendReminderEmail(ticketEvent.StoreId, settledTickets, ticketEvent, settings?.ReminderEmailSubject, settings?.ReminderEmailBody);
                }
                catch (Exception ex)
                {
                    Logs.PayServer.LogWarning(ex, "SatoshiTickets: Failed sending reminders for event {EventId}", ticketEvent.Id);
                }
                var tracked = ctx.Events.First(e => e.Id == ticketEvent.Id);
                tracked.ReminderSentAt = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync();
            }
        }

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
        var order = ctx.Orders.Include(c => c.Tickets).FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id);
        if (order == null) return;

        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        if (order != null && order.PaymentStatus != Data.TransactionStatus.New.ToString())
        {
            result.Write("Transaction has previously been acted on", InvoiceEventData.EventSeverity.Info);
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
            return;
        }
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == order.EventId && c.StoreId == order.StoreId);
        order.PurchaseDate = DateTime.UtcNow;
        order.InvoiceStatus = invoice.Status.ToString().ToLower();
        order.PaymentStatus = success ? Data.TransactionStatus.Settled.ToString() : Data.TransactionStatus.Expired.ToString();
        order.Tickets.ToList().ForEach(c => c.PaymentStatus = success ? Data.TransactionStatus.Settled.ToString() : Data.TransactionStatus.Expired.ToString());
        result.Write($"New ticket payment completed for Event: {ticketEvent?.Title}, Order Id: {order.Id}, Order Txn Id: {order.TxnId}", InvoiceEventData.EventSeverity.Success);

        if (success)
        {
            var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == order.EventId).ToList();
            var ticketCounts = order.Tickets.GroupBy(t => t.TicketTypeId).ToDictionary(g => g.Key, g => g.Count());
            foreach (var ticketType in ticketTypes)
            {
                if (ticketCounts.TryGetValue(ticketType.Id, out var count))
                {
                    ticketType.QuantitySold += count;
                }
            }
        }

        if (success)
        {
            var isEmailConfigured = await _emailService.IsEmailSettingsConfigured(invoice.StoreId);
            if (isEmailConfigured)
            {
                try
                {
                    var emailResponse = await _emailService.SendTicketRegistrationEmail(invoice.StoreId, order.Tickets, ticketEvent);
                    if (emailResponse.IsSuccessful) order.EmailSent = true;
                    result.Write($"Email sent successfully to recipients in Order with Id: {order.Id}", InvoiceEventData.EventSeverity.Success);
                }
                catch { result.Write($"Failed to send email for Order Id: {order.Id}.", InvoiceEventData.EventSeverity.Error); }
            }
        }
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }
}



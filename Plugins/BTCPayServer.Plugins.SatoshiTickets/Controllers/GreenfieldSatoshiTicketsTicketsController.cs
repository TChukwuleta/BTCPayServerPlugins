using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Emails;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Models.Api;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

/// <summary>
/// Greenfield API controller for managing tickets, orders, check-ins, and exports.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketsTicketsController : ControllerBase
{
    private readonly TicketService _ticketService;
    private readonly EmailService _emailService;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;

    public GreenfieldSatoshiTicketsTicketsController(
        TicketService ticketService,
        EmailService emailService,
        EmailSenderFactory emailSenderFactory,
        SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _ticketService = ticketService;
        _emailService = emailService;
        _emailSenderFactory = emailSenderFactory;
        _dbContextFactory = dbContextFactory;
    }

    private string CurrentStoreId => HttpContext.GetStoreData()?.Id;

    /// <summary>
    /// List settled tickets for an event, with optional text search.
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> GetTickets(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var query = ctx.Tickets.AsNoTracking()
            .Where(t => t.EventId == eventId && t.StoreId == CurrentStoreId
                        && t.PaymentStatus == TransactionStatus.Settled.ToString());

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(t =>
                t.TxnNumber.Contains(searchText) ||
                t.FirstName.Contains(searchText) ||
                t.LastName.Contains(searchText) ||
                t.Email.Contains(searchText) ||
                t.TicketNumber.Contains(searchText));
        }

        var tickets = query.ToList();

        var result = tickets.Select(ToTicketData).ToArray();
        return Ok(result);
    }

    /// <summary>
    /// Export settled tickets as a CSV file download.
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets/export")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> ExportTickets(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var ordersWithTickets = ctx.Orders.AsNoTracking()
            .Where(o => o.StoreId == CurrentStoreId && o.EventId == eventId
                        && o.PaymentStatus == TransactionStatus.Settled.ToString())
            .SelectMany(o => o.Tickets.Select(t => new
            {
                o.PurchaseDate,
                t.TxnNumber,
                t.FirstName,
                t.LastName,
                t.Email,
                t.TicketTypeName,
                t.Amount,
                o.Currency,
                t.UsedAt
            })).ToList();

        if (!ordersWithTickets.Any())
            return this.CreateAPIError(404, "no-tickets", "No settled tickets found for this event");

        var fileName = $"{ticketEvent.Title}_Tickets-{DateTime.Now:yyyy_MM_dd-HH_mm_ss}.csv";
        var csvData = new StringBuilder();
        csvData.AppendLine("Purchase Date,Ticket Number,First Name,Last Name,Email,Ticket Tier,Amount,Currency,Attended Event");
        foreach (var ticket in ordersWithTickets)
        {
            csvData.AppendLine(
                $"{ticket.PurchaseDate:MM/dd/yy HH:mm},{ticket.TxnNumber},{ticket.FirstName},{ticket.LastName},{ticket.Email},{ticket.TicketTypeName},{ticket.Amount},{ticket.Currency},{ticket.UsedAt.HasValue}");
        }

        byte[] fileBytes = Encoding.UTF8.GetBytes(csvData.ToString());
        return File(fileBytes, "text/csv", fileName);
    }

    /// <summary>
    /// Check in a ticket by ticket number or short transaction number.
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/tickets/{ticketNumber}/check-in")]
    public async Task<IActionResult> CheckinTicket(string storeId, string eventId, string ticketNumber)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var checkinResult = await _ticketService.CheckinTicket(eventId, ticketNumber, CurrentStoreId);

        var result = new CheckinResultData
        {
            Success = checkinResult.Success,
            ErrorMessage = checkinResult.ErrorMessage,
            Ticket = checkinResult.Ticket != null ? ToTicketData(checkinResult.Ticket) : null
        };

        return Ok(result);
    }

    /// <summary>
    /// List settled orders for an event, with optional text search. Includes nested tickets.
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/orders")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> GetOrders(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var query = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .Where(c => c.EventId == eventId && c.StoreId == CurrentStoreId
                        && c.PaymentStatus == TransactionStatus.Settled.ToString());

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(o =>
                o.InvoiceId.Contains(searchText) ||
                o.Tickets.Any(t =>
                    t.TxnNumber.Contains(searchText) ||
                    t.FirstName.Contains(searchText) ||
                    t.LastName.Contains(searchText) ||
                    t.Email.Contains(searchText)));
        }

        var orders = query.ToList();
        var result = orders.Select(ToOrderData).ToArray();

        return Ok(result);
    }

    /// <summary>
    /// Re-send the ticket confirmation email to a specific ticket holder.
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/orders/{orderId}/tickets/{ticketId}/send-reminder")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string orderId, string ticketId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .FirstOrDefault(o => o.Id == orderId && o.StoreId == CurrentStoreId && o.EventId == eventId && o.Tickets.Any());
        if (order == null)
            return this.CreateAPIError(404, "order-not-found", "The order was not found");

        var ticket = order.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
            return this.CreateAPIError(404, "ticket-not-found", "The ticket was not found");

        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStoreId);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            return this.CreateAPIError(422, "email-not-configured",
                "Email SMTP settings are not configured. Configure email settings in the store admin.");
        }

        try
        {
            var emailResponse = await _emailService.SendTicketRegistrationEmail(CurrentStoreId, ticket, ticketEvent);
            if (emailResponse.IsSuccessful)
            {
                order.EmailSent = true;
                ctx.Orders.Update(order);
                await ctx.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            return this.CreateAPIError(500, "email-send-failed",
                $"An error occurred when sending ticket details: {ex.Message}");
        }

        return Ok(new { success = true, message = "Ticket details have been sent to the recipient via email" });
    }

    private static TicketData ToTicketData(Ticket entity)
    {
        return new TicketData
        {
            Id = entity.Id,
            EventId = entity.EventId,
            TicketTypeId = entity.TicketTypeId,
            TicketTypeName = entity.TicketTypeName,
            Amount = entity.Amount,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Email = entity.Email,
            TicketNumber = entity.TicketNumber,
            TxnNumber = entity.TxnNumber,
            PaymentStatus = entity.PaymentStatus,
            CheckedIn = entity.UsedAt.HasValue,
            CheckedInAt = entity.UsedAt,
            EmailSent = entity.EmailSent,
            CreatedAt = entity.CreatedAt
        };
    }

    private static OrderData ToOrderData(Order entity)
    {
        return new OrderData
        {
            Id = entity.Id,
            EventId = entity.EventId,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            InvoiceId = entity.InvoiceId,
            PaymentStatus = entity.PaymentStatus,
            InvoiceStatus = entity.InvoiceStatus,
            EmailSent = entity.EmailSent,
            CreatedAt = entity.CreatedAt,
            PurchaseDate = entity.PurchaseDate,
            Tickets = entity.Tickets?.Select(ToTicketData).ToList() ?? new()
        };
    }

    private IActionResult EventNotFound()
    {
        return this.CreateAPIError(404, "event-not-found", "The event was not found");
    }
}

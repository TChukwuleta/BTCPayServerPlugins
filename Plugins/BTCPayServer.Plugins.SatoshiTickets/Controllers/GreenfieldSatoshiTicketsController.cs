using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Models.Api;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

[Route("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/")]
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketsController(EmailService emailService, SimpleTicketSalesDbContextFactory dbContextFactory) : ControllerBase
{

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var eventExists = ctx.Events.Any(c => c.Id == eventId && c.StoreId == storeId);
        if (!eventExists)
            return EventNotFound();

        var query = ctx.Tickets.AsNoTracking().Where(t => t.EventId == eventId && t.StoreId == storeId && t.PaymentStatus == TransactionStatus.Settled.ToString());
        if (!string.IsNullOrEmpty(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(t =>
                t.TxnNumber.Contains(searchText) || t.FirstName.Contains(searchText) ||
                t.LastName.Contains(searchText) || t.Email.Contains(searchText) ||t.TicketNumber.Contains(searchText));
        }
        var tickets = query.ToList();
        var result = tickets.Select(ToTicketData).ToArray();
        return Ok(result);
    }


    /*[HttpPost("tickets/{ticketNumber}/check-in")]
    public async Task<IActionResult> CheckinTicket(string storeId, string eventId, string ticketNumber)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var ticketExist = ctx.Events.Any(c => c.Id == eventId && c.StoreId == storeId);
        if (!ticketExist)
            return EventNotFound();

        var checkinResult = await ticketService.CheckinTicket(eventId, ticketNumber, storeId);
        if (!checkinResult.Success)
            return this.CreateAPIError(422, "checkin-failed", checkinResult.ErrorMessage);

        var result = new CheckinResultData
        {
            Success = checkinResult.Success,
            ErrorMessage = checkinResult.ErrorMessage,
            Ticket = checkinResult.Ticket != null ? ToTicketData(checkinResult.Ticket) : null
        };
        return Ok(result);
    }*/


    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(string storeId, string eventId, [FromQuery] string searchText = null)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var ticketExist = ctx.Events.Any(c => c.Id == eventId && c.StoreId == storeId);
        if (!ticketExist)
            return EventNotFound();

        var query = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .Where(c => c.EventId == eventId && c.StoreId == storeId && c.PaymentStatus == TransactionStatus.Settled.ToString());

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

    [HttpPost("orders/{orderId}/tickets/{ticketId}/send-reminder")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string orderId, string ticketId)
    {
        await using var ctx = dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (ticketEvent == null)
            return EventNotFound();

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .FirstOrDefault(o => o.Id == orderId && o.StoreId == storeId && o.EventId == eventId && o.Tickets.Any());
        if (order == null)
            return this.CreateAPIError(404, "order-not-found", "The order was not found");

        var ticket = order.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
            return this.CreateAPIError(404, "ticket-not-found", "The ticket was not found");

        var isEmailConfigured = await emailService.IsEmailSettingsConfigured(storeId);
        if (!isEmailConfigured)
        {
            return this.CreateAPIError(422, "email-not-configured",
                "Email SMTP settings are not configured. Configure email settings in the store admin.");
        }
        try
        {
            var emailResponse = await emailService.SendTicketRegistrationEmail(storeId, ticket, ticketEvent);
            if (emailResponse.IsSuccessful)
            {
                order.EmailSent = true;
                ctx.Orders.Update(order);
                await ctx.SaveChangesAsync();
            }
            else
            {
                var failedList = emailResponse.FailedRecipients?.Count > 0
                    ? string.Join(", ", emailResponse.FailedRecipients) : ticket.Email;
                return this.CreateAPIError(500, "email-send-failed", $"Failed to send ticket email to: {failedList}");
            }
        }
        catch (Exception ex)
        {
            return this.CreateAPIError(500, "email-send-failed", $"An error occurred when sending ticket details: {ex.Message}");
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

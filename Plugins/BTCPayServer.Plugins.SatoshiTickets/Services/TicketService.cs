using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public class TicketService
{
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    public TicketService(SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TicketCheckinResponse> CheckinTicket(string eventId, string ticketId, string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null) return new() { ErrorMessage = "Invalid Event specified", Success = false };

        var ticket = ctx.Tickets.FirstOrDefault(c => c.Id == ticketId && c.EventId == entity.Id);
        if (ticket == null) return new() { ErrorMessage = "Invalid ticket record specified", Success = false };

        if (ticket.UsedAt.HasValue) return new() { ErrorMessage = $"Ticket previously checked in by {ticket.UsedAt.Value:f}", Success = false, Ticket = ticket };
        try
        {
            ticket.UsedAt = DateTime.UtcNow;
            ctx.Tickets.Update(ticket);
            await ctx.SaveChangesAsync();
        }
        catch (Exception)
        {
            return new() { Success = false, ErrorMessage = "Error occured while checking in ticket" };
        }
        return new() { Success = true, Ticket = ticket };   
    }
}


public class TicketCheckinResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public Ticket Ticket { get; set; }
}
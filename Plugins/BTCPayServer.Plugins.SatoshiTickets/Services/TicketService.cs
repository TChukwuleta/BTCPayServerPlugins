using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public class TicketService(SimpleTicketSalesDbContextFactory dbContextFactory, InvoiceRepository invoiceRepository)
{

    public async Task<TicketCheckinResponse> CheckinTicket(string eventId, string ticketNumber, string storeId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null) return new() { ErrorMessage = "Invalid Event specified", Success = false };

        var ticket = ctx.Tickets.FirstOrDefault(c => (c.TicketNumber == ticketNumber || c.TxnNumber == ticketNumber) && c.EventId == entity.Id);
        if (ticket == null) return new() { ErrorMessage = "Invalid ticket record specified", Success = false };

        if (ticket.UsedAt.HasValue) return new() { ErrorMessage = $"Ticket previously checked in by {ticket.UsedAt.Value:f}", Success = false, Ticket = ticket };

        var rowsAffected = await ctx.Tickets.Where(t => (t.TicketNumber == ticketNumber || t.TxnNumber == ticketNumber) && t.EventId == eventId && t.StoreId == storeId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow));

        if (rowsAffected == 0)
            return new() { Success = false, ErrorMessage = "Ticket was already checked in" };

        ticket.UsedAt = DateTime.UtcNow;
        return new() { Success = true, Ticket = ticket };  
    }

    public async Task<(byte[] data, string fileName)?> ExportTicketsCsv(string storeId, string eventId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (ticketEvent == null) return null;

        var orders = ctx.Orders.AsNoTracking().Where(o => o.StoreId == storeId && o.EventId == eventId && o.PaymentStatus == TransactionStatus.Settled.ToString())
            .Include(o => o.Tickets).ToList();
        if (!orders.Any()) return null;

        var invoiceIds = orders.Select(o => o.InvoiceId).Distinct().ToArray();
        var invoices = (await invoiceRepository.GetInvoices(new InvoiceQuery
        {
            InvoiceId = invoiceIds,
            StoreId = new[] { storeId }
        })).ToDictionary(i => i.Id);

        var fileName = $"{ticketEvent.Title}_Tickets-{DateTime.Now:yyyy_MM_dd-HH_mm_ss}.csv";
        var csvData = new StringBuilder();
        csvData.AppendLine("Purchase Date,Ticket Number,First Name,Last Name,Email,Ticket Tier,Amount,Currency,Crypto Currency,Crypto Amount Paid,Attended Event");

        foreach (var order in orders)
        {
            invoices.TryGetValue(order.InvoiceId, out var invoice);
            var payments = invoice?.GetPayments(true).Where(p => p.Accounted).ToList();
            var cryptoCurrency = payments?.FirstOrDefault()?.Currency ?? "";
            var totalCryptoPaid = payments?.Sum(p => p.PaidAmount.Net) ?? 0m;
            var totalFiatAmount = order.Tickets.Sum(t => t.Amount);

            foreach (var ticket in order.Tickets)
            {
                var proportion = totalFiatAmount > 0 ? ticket.Amount / totalFiatAmount : 0m;
                var cryptoForTicket = Math.Round(totalCryptoPaid * proportion, 8);
                csvData.AppendLine($"{order.PurchaseDate:MM/dd/yy HH:mm},{ticket.TxnNumber},{ticket.FirstName},{ticket.LastName},{ticket.Email},{ticket.TicketTypeName},{ticket.Amount},{order.Currency},{cryptoCurrency},{cryptoForTicket},{ticket.UsedAt.HasValue}");
            }
        }
        return (Encoding.UTF8.GetBytes(csvData.ToString()), fileName);
    }
}


public class TicketCheckinResponse
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public Ticket Ticket { get; set; }
}
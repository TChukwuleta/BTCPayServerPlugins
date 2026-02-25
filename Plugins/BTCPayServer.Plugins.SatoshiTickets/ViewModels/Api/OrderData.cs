using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class OrderData
{
    public string Id { get; set; }
    public string EventId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; }
    public string InvoiceId { get; set; }
    public string PaymentStatus { get; set; }
    public string InvoiceStatus { get; set; }
    public bool EmailSent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public List<TicketData> Tickets { get; set; } = new();
}

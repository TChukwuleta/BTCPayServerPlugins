using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Response model representing a ticket order with nested tickets.
/// </summary>
public class OrderData
{
    /// <summary>Unique order identifier.</summary>
    public string Id { get; set; }
    /// <summary>Event identifier this order belongs to.</summary>
    public string EventId { get; set; }
    /// <summary>Total order amount.</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>Currency code for the order.</summary>
    public string Currency { get; set; }
    /// <summary>BTCPay Server invoice identifier.</summary>
    public string InvoiceId { get; set; }
    /// <summary>Payment status (e.g. "Settled").</summary>
    public string PaymentStatus { get; set; }
    /// <summary>Invoice status from BTCPay Server.</summary>
    public string InvoiceStatus { get; set; }
    /// <summary>Whether the confirmation email has been sent.</summary>
    public bool EmailSent { get; set; }
    /// <summary>Timestamp when the order was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Timestamp when the payment was settled.</summary>
    public DateTimeOffset? PurchaseDate { get; set; }
    /// <summary>List of tickets in this order.</summary>
    public List<TicketData> Tickets { get; set; } = new();
}

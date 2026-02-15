using System;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Response model representing a purchased ticket.
/// </summary>
public class TicketData
{
    /// <summary>Unique ticket identifier (UUID).</summary>
    public string Id { get; set; }
    /// <summary>Event identifier this ticket belongs to.</summary>
    public string EventId { get; set; }
    /// <summary>Ticket type identifier.</summary>
    public string TicketTypeId { get; set; }
    /// <summary>Name of the ticket type (e.g. "VIP", "Standard").</summary>
    public string TicketTypeName { get; set; }
    /// <summary>Ticket price amount.</summary>
    public decimal Amount { get; set; }
    /// <summary>Ticket holder's first name.</summary>
    public string FirstName { get; set; }
    /// <summary>Ticket holder's last name.</summary>
    public string LastName { get; set; }
    /// <summary>Ticket holder's email address.</summary>
    public string Email { get; set; }
    /// <summary>Full ticket number (e.g. "EVT-abc123-260615-xK9mP2qR").</summary>
    public string TicketNumber { get; set; }
    /// <summary>Short transaction number (e.g. "xK9mP2qR").</summary>
    public string TxnNumber { get; set; }
    /// <summary>Payment status (e.g. "Settled").</summary>
    public string PaymentStatus { get; set; }
    /// <summary>Whether the ticket has been checked in at the event.</summary>
    public bool CheckedIn { get; set; }
    /// <summary>Timestamp when the ticket was checked in (null if not checked in).</summary>
    public DateTimeOffset? CheckedInAt { get; set; }
    /// <summary>Whether the confirmation email has been sent.</summary>
    public bool EmailSent { get; set; }
    /// <summary>Timestamp when the ticket was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

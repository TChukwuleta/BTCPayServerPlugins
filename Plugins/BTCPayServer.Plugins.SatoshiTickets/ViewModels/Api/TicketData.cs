using System;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class TicketData
{
    public string Id { get; set; }
    public string EventId { get; set; }
    public string TicketTypeId { get; set; }
    public string TicketTypeName { get; set; }
    public decimal Amount { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string TicketNumber { get; set; }
    public string TxnNumber { get; set; }
    public string PaymentStatus { get; set; }
    public bool CheckedIn { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public bool EmailSent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

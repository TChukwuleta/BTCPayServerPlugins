using System;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Response model representing an event.
/// </summary>
public class EventData
{
    /// <summary>Unique event identifier.</summary>
    public string Id { get; set; }
    /// <summary>Store identifier the event belongs to.</summary>
    public string StoreId { get; set; }
    /// <summary>Event title.</summary>
    public string Title { get; set; }
    /// <summary>Event description (may contain HTML).</summary>
    public string Description { get; set; }
    /// <summary>Event type: "Physical" or "Virtual".</summary>
    public string EventType { get; set; }
    /// <summary>Event venue or location.</summary>
    public string Location { get; set; }
    /// <summary>Event start date in UTC.</summary>
    public DateTime StartDate { get; set; }
    /// <summary>Event end date in UTC (optional).</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>Three-letter currency code (e.g. "EUR", "USD", "BTC").</summary>
    public string Currency { get; set; }
    /// <summary>URL to redirect the customer after successful payment.</summary>
    public string RedirectUrl { get; set; }
    /// <summary>Email subject template for ticket confirmation emails.</summary>
    public string EmailSubject { get; set; }
    /// <summary>Email body template with placeholder support (e.g. {{Name}}, {{Title}}).</summary>
    public string EmailBody { get; set; }
    /// <summary>Whether the event has a total ticket capacity limit.</summary>
    public bool HasMaximumCapacity { get; set; }
    /// <summary>Maximum total tickets when <see cref="HasMaximumCapacity"/> is true.</summary>
    public int? MaximumEventCapacity { get; set; }
    /// <summary>Current event state: "Active" or "Disabled".</summary>
    public string EventState { get; set; }
    /// <summary>File ID of the uploaded event logo image.</summary>
    public string EventLogoFileId { get; set; }
    /// <summary>Fully resolved public URL of the event logo image.</summary>
    public string EventLogoUrl { get; set; }
    /// <summary>Public URL where customers can purchase tickets.</summary>
    public string PurchaseLink { get; set; }
    /// <summary>Number of settled (paid) tickets sold.</summary>
    public int TicketsSold { get; set; }
    /// <summary>Timestamp when the event was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

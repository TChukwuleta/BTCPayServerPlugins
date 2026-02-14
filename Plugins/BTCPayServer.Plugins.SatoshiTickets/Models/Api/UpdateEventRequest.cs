using System;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Request model for updating an existing event.
/// </summary>
public class UpdateEventRequest
{
    /// <summary>Event title (required).</summary>
    public string Title { get; set; }
    /// <summary>Event description (HTML allowed).</summary>
    public string Description { get; set; }
    /// <summary>Event type: "Physical" or "Virtual".</summary>
    public string EventType { get; set; }
    /// <summary>Event venue or location.</summary>
    public string Location { get; set; }
    /// <summary>Event start date in ISO 8601 format.</summary>
    public DateTime StartDate { get; set; }
    /// <summary>Event end date in ISO 8601 format. Must be after StartDate if provided.</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>Three-letter currency code.</summary>
    public string Currency { get; set; }
    /// <summary>URL to redirect the customer after successful payment.</summary>
    public string RedirectUrl { get; set; }
    /// <summary>Email subject template for ticket confirmation emails.</summary>
    public string EmailSubject { get; set; }
    /// <summary>Email body template. Supports placeholders: {{Name}}, {{Title}}, {{Location}}, {{Email}}, {{Description}}, {{EventDate}}, {{Currency}}.</summary>
    public string EmailBody { get; set; }
    /// <summary>Whether the event has a total ticket capacity limit.</summary>
    public bool HasMaximumCapacity { get; set; }
    /// <summary>Maximum total tickets. Required when HasMaximumCapacity is true.</summary>
    public int? MaximumEventCapacity { get; set; }
    /// <summary>File ID to set/change logo. Send empty string to remove logo. Omit or send null to leave unchanged.</summary>
    public string EventLogoFileId { get; set; }
}

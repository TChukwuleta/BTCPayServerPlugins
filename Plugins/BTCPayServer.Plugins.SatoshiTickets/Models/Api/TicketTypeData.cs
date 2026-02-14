namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Response model representing a ticket type (pricing tier).
/// </summary>
public class TicketTypeData
{
    /// <summary>Unique ticket type identifier.</summary>
    public string Id { get; set; }
    /// <summary>Event identifier this ticket type belongs to.</summary>
    public string EventId { get; set; }
    /// <summary>Ticket type name (e.g. "VIP", "Standard").</summary>
    public string Name { get; set; }
    /// <summary>Price per ticket.</summary>
    public decimal Price { get; set; }
    /// <summary>Description of what this tier includes.</summary>
    public string Description { get; set; }
    /// <summary>Total number of tickets available for this type.</summary>
    public int Quantity { get; set; }
    /// <summary>Number of tickets already sold.</summary>
    public int QuantitySold { get; set; }
    /// <summary>Number of tickets still available (Quantity - QuantitySold).</summary>
    public int QuantityAvailable { get; set; }
    /// <summary>Whether this is the pre-selected default ticket type.</summary>
    public bool IsDefault { get; set; }
    /// <summary>Current ticket type state: "Active" or "Disabled".</summary>
    public string TicketTypeState { get; set; }
}

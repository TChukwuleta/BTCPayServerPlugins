namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Request model for updating an existing ticket type.
/// </summary>
public class UpdateTicketTypeRequest
{
    /// <summary>Ticket type name (required).</summary>
    public string Name { get; set; }
    /// <summary>Price per ticket. Must be greater than 0 (required).</summary>
    public decimal Price { get; set; }
    /// <summary>Description of what this tier includes.</summary>
    public string Description { get; set; }
    /// <summary>Number of tickets available. Must be greater than 0 when event has maximum capacity.</summary>
    public int Quantity { get; set; }
    /// <summary>Whether this should be the pre-selected default ticket type.</summary>
    public bool IsDefault { get; set; }
}

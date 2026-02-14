namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

/// <summary>
/// Response model for a ticket check-in operation.
/// </summary>
public class CheckinResultData
{
    /// <summary>Whether the check-in was successful.</summary>
    public bool Success { get; set; }
    /// <summary>Error message if check-in failed (e.g. already checked in, invalid ticket).</summary>
    public string ErrorMessage { get; set; }
    /// <summary>The ticket data (null if ticket was not found).</summary>
    public TicketData Ticket { get; set; }
}

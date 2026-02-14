namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class TicketTypeData
{
    public string Id { get; set; }
    public string EventId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public int QuantityAvailable { get; set; }
    public bool IsDefault { get; set; }
    public string TicketTypeState { get; set; }
}

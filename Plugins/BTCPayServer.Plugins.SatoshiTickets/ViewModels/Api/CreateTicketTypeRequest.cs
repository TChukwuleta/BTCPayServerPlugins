namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class CreateTicketTypeRequest
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public bool IsDefault { get; set; }
}

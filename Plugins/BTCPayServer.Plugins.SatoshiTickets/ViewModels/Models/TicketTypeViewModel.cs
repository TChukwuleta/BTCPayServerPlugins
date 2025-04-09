using System.Collections.Generic;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class TicketTypeViewModel
{
    public string TicketTypeId { get; set; }
    public bool TicketHasMaximumCapacity { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public int QuantityAvailable { get; set; }
    public string EventId { get; set; }
    public EntityState TicketTypeState { get; set; }
    public bool IsDefault { get; set; }
}

public class TicketTypeListViewModel
{
    public string EventId { get; set; }
    public List<TicketTypeViewModel> TicketTypes { get; set; }
}

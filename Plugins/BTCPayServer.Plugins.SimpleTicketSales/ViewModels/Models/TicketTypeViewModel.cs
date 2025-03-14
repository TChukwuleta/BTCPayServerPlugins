using System.Collections.Generic;
using BTCPayServer.Plugins.SimpleTicketSales.Data;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class TicketTypeViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public string EventId { get; set; }
    public EntityState TicketTypeState { get; set; }
}

public class TicketTypeListViewModel
{
    public string EventId { get; set; }
    public List<TicketTypeViewModel> TicketTypes { get; set; }
}

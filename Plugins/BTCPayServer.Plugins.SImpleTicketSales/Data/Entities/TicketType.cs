using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class TicketType
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public bool IsDefault { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int Quantity { get; set; }
    public int QuantitySold { get; set; }
    public string EventId { get; set; }
    public EntityState TicketTypeState { get; set; }
}

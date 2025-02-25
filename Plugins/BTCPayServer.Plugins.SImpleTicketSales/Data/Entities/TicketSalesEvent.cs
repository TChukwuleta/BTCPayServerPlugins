using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class TicketSalesEvent
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string EventImageUrl { get; set; }
    public string EventLink { get; set; }
    public DateTime EventDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }
    public bool HasMaximumCapacity { get; set; }
    public int? MaximumEventCapacity { get; set; }
}

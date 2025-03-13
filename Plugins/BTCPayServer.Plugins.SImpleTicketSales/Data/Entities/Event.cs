using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class Event
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string EventLogo { get; set; }
    public EventType EventType { get; set; }
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public string Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }
    public bool HasMaximumCapacity { get; set; }
    public int? MaximumEventCapacity { get; set; }
    public EntityState EventState { get; set; }
}

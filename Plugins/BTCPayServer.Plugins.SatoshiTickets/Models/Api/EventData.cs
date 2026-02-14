using System;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.Models.Api;

public class EventData
{
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string EventType { get; set; }
    public string Location { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Currency { get; set; }
    public string RedirectUrl { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }
    public bool HasMaximumCapacity { get; set; }
    public int? MaximumEventCapacity { get; set; }
    public string EventState { get; set; }
    public string EventLogoFileId { get; set; }
    public string EventLogoUrl { get; set; }
    public string PurchaseLink { get; set; }
    public int TicketsSold { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

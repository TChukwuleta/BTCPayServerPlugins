using System.Collections.Generic;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class TicketPurchaseRequestVm
{
    public string StoreId { get; set; }
    public decimal TotalAmount { get; set; }
    public string RedirectUrl { get; set; }
    public List<TicketRequestVm> Tickets { get; set; }
}

public class TicketRequestVm
{
    public string EventId { get; set; }
    public string TicketTypeId { get; set; }
    public string AttendeeName { get; set; }
    public string AttendeeEmail { get; set; }
}
using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class TicketViewModel : BaseSimpleTicketPublicViewModel
{
    public string EventName { get; set; }
    public string Location { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public DateTimeOffset PurchaseDate { get; set; }
    public List<TicketListViewModel> Tickets { get; set; }
}

public class TicketListViewModel
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public string TicketNumber { get; set; }
    public string TicketType { get; set; }
}
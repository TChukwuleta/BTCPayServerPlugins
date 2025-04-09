using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

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
    public string TicketId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public string TicketNumber { get; set; }
    public string TicketType { get; set; }
    public string QrCodeUrl { get; set; }
}

public class TicketVerificationViewModel
{
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public string QrCodeData { get; set; }
    public string StoreId { get; set; }
}

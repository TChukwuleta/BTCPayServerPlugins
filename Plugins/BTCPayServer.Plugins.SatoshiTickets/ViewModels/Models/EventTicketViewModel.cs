using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class EventTicketViewModel
{
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public string StoreId { get; set; }
    public string SearchText { get; set; }
    public List<EventTicketOrdersVm> TicketOrders { get; set; }
}

public class EventTicketOrdersVm
{
    public string OrderId { get; set; }
    public int Quantity { get; set; }
    public string InvoiceId { get; set; }
    public bool HasEmailNotificationBeenSent { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public DateTimeOffset PurchaseDate { get; set; }
    public List<EventContactPersonTicketVm> Tickets { get; set; }
}

public class EventContactPersonTicketVm
{
    public string Id { get; set; }
    public decimal Amount { get; set; }
    public string TicketTypeId { get; set; }
    public string TicketTypeName { get; set; }
    public string TicketNumber { get; set; }
    public string Currency { get; set; }
    public DateTimeOffset CreatedDate { get; set; }

}
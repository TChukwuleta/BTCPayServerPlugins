using System;
using System.Collections.Generic;
using BTCPayServer.Plugins.SimpleTicketSales.Data;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class SalesTicketsEventsListViewModel
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string EventPurchaseLink { get; set; }
    public DateTime EventDate { get; set; }
    public string StoreId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Location { get; set; }
    public EntityState EventState { get; set; }
    public bool IsPublished { get; set; }
    public long TicketSold { get; set; }
}

public class SalesTicketTransactionViewModel
{
    public string StoreId { get; set; }
    public string InvoiceId { get; set; }
    public string PaymentRequestId { get; set; }
    public string InvoiceStatus { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string MemberId { get; set; }
    public DateTimeOffset PeriodStartDate { get; set; }
    public DateTimeOffset PeriodEndDate { get; set; }
}

public class SalesTicketEventTicketsViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Email { get; set; }
    public string InvoiceId { get; set; }
    public string PaymentStatus { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public class SalesTicketsEventsViewModel
{
    public List<SalesTicketsEventsListViewModel> Events { get; set; }
    public List<SalesTicketsEventsListViewModel> DisplayedEvents { get; set; }
    public bool Active { get; set; }
    public bool Expired { get; set; }
}

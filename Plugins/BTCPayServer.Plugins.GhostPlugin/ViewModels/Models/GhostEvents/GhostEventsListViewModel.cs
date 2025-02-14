using System;
using System.Collections.Generic;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

public class GhostEventsListViewModel
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string EventPurchaseLink { get; set; }
    public DateTime EventDate { get; set; }
    public string StoreId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string EventLink { get; set; }
    public List<GhostTransactionViewModel> Subscriptions { get; set; }
    public List<GhostEventTicketsViewModel> Tickets { get; set; }
}

public class GhostEventTicketsViewModel
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

public class GhostEventsViewModel
{
    public List<GhostEventsListViewModel> Events { get; set; }
    public List<GhostEventsListViewModel> DisplayedEvents { get; set; }
    public bool Active { get; set; }
    public bool Expired { get; set; }
}

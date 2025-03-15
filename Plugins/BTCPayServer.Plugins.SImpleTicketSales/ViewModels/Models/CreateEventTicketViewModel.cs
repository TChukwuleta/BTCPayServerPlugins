using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.SimpleTicketSales.Data;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class CreateEventTicketViewModel : BaseSimpleTicketPublicViewModel
{
    [Display(Name = "Email Address")]
    public string Email { get; set; }

    [Display(Name = "Full Name")]
    public string Name { get; set; }
    public string EventTitle { get; set; }
    public DateTime EventDate { get; set; }
    public string EventImageUrl { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string EventId { get; set; }
    public string Location { get; set; }
    public EventType EventType { get; set; }
    public string SelectedTierId { get; set; }
    public List<TicketTypePurchaseViewModel> TicketTypes { get; set; }
    public int Quantity { get; set; }
    public string FormattedEventDate => EventDate.ToString("dddd, MMMM d yyyy");
    public string FormattedEventTime => EventDate.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    /*public DateTime EventEndDate { get; set; }

    public string FormattedEventTime => EventStartDate.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
                                      + " - " +
                                      EventEndDate.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
                                      + " UTC";*/
}

public class TicketTypePurchaseViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public int QuantityAvailable { get; set; }
    public string EventId { get; set; }
}

public class EventSummaryViewModel : BaseSimpleTicketPublicViewModel
{
    public string EventTitle { get; set; }
    public DateTime EventDate { get; set; }
    public string EventImageUrl { get; set; }
    public string Description { get; set; }
    public string EventId { get; set; }
    public EventType EventType { get; set; }
    public string FormattedEventDate => EventDate.ToString("dddd, MMMM d yyyy");
    public string FormattedEventTime => EventDate.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
}

public class EventTicketPageViewModel : BaseSimpleTicketPublicViewModel
{
    public string EventTitle { get; set; }
    public string Currency { get; set; }
    public DateTime EventDate { get; set; }
    public string EventImageUrl { get; set; }
    public string Description { get; set; }
    public string EventId { get; set; }
    public string Location { get; set; }
    public EventType EventType { get; set; }
    public List<TicketSelectionViewModel> Tickets { get; set; } = new List<TicketSelectionViewModel>();
    public List<TicketTypeViewModel> TicketTypes { get; set; }
}

public class ContactInfoPageViewModel : BaseSimpleTicketPublicViewModel
{
    public string Currency { get; set; }
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public List<TicketSelectionViewModel> Tickets { get; set; }
    public List<TicketContactInfoViewModel> ContactInfo { get; set; } = new List<TicketContactInfoViewModel>();
}

public class TicketPageViewModel
{
    public int StoreId { get; set; }
    public int EventId { get; set; }
    public List<TicketSelectionViewModel> Tickets { get; set; }
}

public class TicketSelectionViewModel
{
    public string TicketTypeId { get; set; }
    public string TicketTypeName { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
public class TicketContactInfoViewModel
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string TicketTypeId { get; set; }
    public int Quantity { get; set; }
}


public class TicketOrderViewModel
{
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public List<TicketSelectionViewModel> Tickets { get; set; } = new List<TicketSelectionViewModel>();
    public List<TicketContactInfoViewModel> ContactInfo { get; set; } = new List<TicketContactInfoViewModel>();
    public bool IsStepOneComplete { get; set; } // Tickets
    public bool IsStepTwoComplete { get; set; } // Contact
    public bool IsStepThreeComplete { get; set; } // Payment
}



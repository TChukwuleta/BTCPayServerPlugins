using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class UpdateSimpleTicketSalesEventViewModel
{
    public string EventId { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }

    [Display(Name = "Event Type")]
    public EventType EventType { get; set; }
    public List<SelectListItem> EventTypes { get; set; }
    public string Description { get; set; }

    [Display(Name = "Event Image URL")]
    public string EventImageUrl { get; set; }

    [Display(Name = "Event Image URL")]
    [JsonIgnore]
    public IFormFile EventImageFile { get; set; }

    [Display(Name = "Event location or URL")]
    public string Location { get; set; }

    [Display(Name = "Event Start Date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Display(Name = "Event End Date")]
    public DateTime? EndDate { get; set; }
    public string StoreDefaultCurrency { get; set; }
    public string Currency { get; set; }

    [Display(Name = "Email Subject")]
    public string EmailSubject { get; set; }

    [Display(Name = "Email Body")]
    public string EmailBody { get; set; }

    [Display(Name = "Limit Ticket Sales")]
    public bool HasMaximumCapacity { get; set; }

    [Display(Name = "Maximum number of ticket for sale")]
    public int? MaximumEventCapacity { get; set; }

    [Display(Name = "Redirect Url after ticket purchase")]
    public string RedirectUrl { get; set; }
}

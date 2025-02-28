using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class UpdateSimpleTicketSalesEventViewModel
{
    public string EventId { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    [Display(Name = "Event Image URL")]
    public string EventImageUrl { get; set; }

    [Display(Name = "Event Image URL")]
    [JsonIgnore]
    public IFormFile EventImageFile { get; set; }

    [Display(Name = "Event Link/Address")]
    public string EventLink { get; set; }
    public DateTime EventDate { get; set; } = DateTime.UtcNow.Date;
    public string StoreDefaultCurrency { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    [Display(Name = "Email Subject")]
    public string EmailSubject { get; set; }

    [Display(Name = "Email Body")]
    public string EmailBody { get; set; }

    [Display(Name = "Limit Ticket Sales")]
    public bool HasMaximumCapacity { get; set; }

    [Display(Name = "Maximum number of ticket for sale")]
    public int? MaximumEventCapacity { get; set; }
    public string EventPaymentUrl { get; set; }
}

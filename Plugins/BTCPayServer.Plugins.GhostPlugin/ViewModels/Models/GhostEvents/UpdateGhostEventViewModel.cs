using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class UpdateGhostEventViewModel
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

    [Display(Name = "Event Registration Link")]
    public string EventLink { get; set; }
    public DateTime EventDate { get; set; }
    public string StoreDefaultCurrency { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }

    [Display(Name = "Limit Event Attendance")]
    public bool HasMaximumCapacity { get; set; }
    public int? MaximumEventCapacity { get; set; }
    public string EventPaymentUrl { get; set; }
}

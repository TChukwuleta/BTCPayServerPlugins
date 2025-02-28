using System;
using System.ComponentModel.DataAnnotations;

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
}

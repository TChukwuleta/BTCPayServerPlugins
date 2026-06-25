using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.SatoshiTickets.Data;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;


public class DiscountCodeListViewModel
{
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public string Currency { get; set; }
    public List<DiscountCodeListItemViewModel> Codes { get; set; } = new();
}

public class DiscountCodeListItemViewModel
{
    public string Id { get; set; }
    public string Code { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public string TicketTypeName { get; set; }
    public int? MaxUses { get; set; }
    public int UsesCount { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public DiscountCodeState DiscountCodeState { get; set; }
}

public class UpsertDiscountCodeViewModel
{
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string EventTitle { get; set; }
    public string Currency { get; set; }

    [Required]
    [Display(Name = "Discount Code")]
    public string Code { get; set; }

    [Display(Name = "Discount Type")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Display(Name = "Value")]
    [Range(0.01, 1000000)]
    public decimal Value { get; set; }

    // Empty => applies to all ticket types in the event.
    [Display(Name = "Applies To Ticket Type")]
    public string TicketTypeId { get; set; }

    [Display(Name = "Maximum Uses (leave blank for unlimited)")]
    public int? MaxUses { get; set; }

    [Display(Name = "Minimum Eligible Quantity (optional)")]
    public int? MinQuantity { get; set; }

    [Display(Name = "Expiry Date (optional)")]
    public DateTimeOffset? ExpiryDate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // For the ticket-type dropdown.
    public List<DiscountTicketTypeOption> TicketTypeOptions { get; set; } = new();
}

public class DiscountTicketTypeOption
{
    public string Id { get; set; }
    public string Name { get; set; }
}

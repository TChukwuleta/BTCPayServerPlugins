using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SquareSpace.ViewModels;

public class SquareSpaceCheckoutViewModel
{
    public string StoreId { get; set; }
    public string CartToken { get; set; }
    public string ShippingName { get; set; }
    public decimal Amount { get; set; }
    public List<InvoiceItem> Items { get; set; }
    [Required]
    public string FirstName { get; set; }

    [Required]
    public string LastName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Address1 { get; set; }

    public string Address2 { get; set; }

    [Required]
    public string City { get; set; }

    [Required]
    public string PostalCode { get; set; }

    [Required]
    public string Country { get; set; }
}

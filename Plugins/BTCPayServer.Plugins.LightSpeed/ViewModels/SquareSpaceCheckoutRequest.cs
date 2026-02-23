using System.Collections.Generic;

namespace BTCPayServer.Plugins.SquareSpace.ViewModels;

public class SquareSpaceCheckoutRequest
{
    public string CartId { get; set; }
    public string CartData { get; set; }
    public string CartToken { get; set; }
    public string CustomerEmail { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public List<InvoiceItem> Items { get; set; }
}

public class InvoiceItem
{
    public string Title { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ShippingAddress
{
    public string ShippingName { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string City { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
}
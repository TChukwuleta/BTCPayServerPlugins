using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;

public class ShopifyOrderVm
{
    public long Id { get; set; }
    public string AdminGraphqlApiId { get; set; }
    public long AppId { get; set; }
    public string BrowserIp { get; set; }
    public bool BuyerAcceptsMarketing { get; set; }
    public string CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CartToken { get; set; }
    public long CheckoutId { get; set; }
    public string CheckoutToken { get; set; }
    public ClientDetails ClientDetails { get; set; }
    public string ConfirmationNumber { get; set; }
    public bool Confirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Currency { get; set; }
    public string CurrentSubtotalPrice { get; set; }
    public PriceSet CurrentSubtotalPriceSet { get; set; }
    public string CurrentTotalPrice { get; set; }
    public PriceSet CurrentTotalPriceSet { get; set; }
    public string FinancialStatus { get; set; }
    public FulfillmentStatus FulfillmentStatus { get; set; }
    public string LandingSite { get; set; }
    public string Name { get; set; }
    public int Number { get; set; }
    public string OrderNumber { get; set; }
    public List<LineItem> LineItems { get; set; }
    public List<ShippingLine> ShippingLines { get; set; }
    public BillingAddress BillingAddress { get; set; }
    public ShippingAddress ShippingAddress { get; set; }
    public Customer Customer { get; set; }
}


public class ClientDetails
{
    public string AcceptLanguage { get; set; }
    public string BrowserIp { get; set; }
    public string UserAgent { get; set; }
}

public class PriceSet
{
    public Money ShopMoney { get; set; }
    public Money PresentmentMoney { get; set; }
}

public class Money
{
    public string Amount { get; set; }
    public string CurrencyCode { get; set; }
}

public class TotalDiscountSet
{
    public Money ShopMoney { get; set; }
    public Money PresentmentMoney { get; set; }
}

public class FulfillmentStatus
{
}

public class LineItem
{
    public long Id { get; set; }
    public string AdminGraphqlApiId { get; set; }
    public int CurrentQuantity { get; set; }
    public int FulfillableQuantity { get; set; }
    public string FulfillmentService { get; set; }
    public string FulfillmentStatus { get; set; }
    public bool GiftCard { get; set; }
    public int Grams { get; set; }
    public string Name { get; set; }
    public string Price { get; set; }
    public PriceSet PriceSet { get; set; }
    public bool ProductExists { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public bool RequiresShipping { get; set; }
    public bool Taxable { get; set; }
    public string Title { get; set; }
    public string TotalDiscount { get; set; }
    public TotalDiscountSet TotalDiscountSet { get; set; }
    public long VariantId { get; set; }
    public string VariantInventoryManagement { get; set; }
    public string Vendor { get; set; }
    public List<TaxLine> TaxLines { get; set; }
    public List<Duty> Duties { get; set; }
    public List<DiscountAllocation> DiscountAllocations { get; set; }
}

public class ShippingLine
{
    public long Id { get; set; }
    public string CarrierIdentifier { get; set; }
    public string Code { get; set; }
    public PriceSet PriceSet { get; set; }
}

public class BillingAddress
{
    public string Country { get; set; }
    public string CountryCode { get; set; }
}

public class ShippingAddress
{
    public string Country { get; set; }
    public string CountryCode { get; set; }
}

public class Customer
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string State { get; set; }
    public bool VerifiedEmail { get; set; }
    public BillingAddress DefaultAddress { get; set; }
}

public class TaxLine { }

public class Duty { }

public class DiscountAllocation { }
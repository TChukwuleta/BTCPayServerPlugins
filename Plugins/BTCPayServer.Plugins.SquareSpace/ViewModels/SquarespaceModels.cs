using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SquareSpace.ViewModels;

public class SquarespaceSettingsVm
{
    [Required]
    [Display(Name = "Squarespace OAuth Token")]
    public string OAuthToken { get; set; }

    [Required]
    [Display(Name = "Squarespace Website ID")]
    public string WebsiteId { get; set; }

    [Display(Name = "Webhook Endpoint URL")]
    public string WebhookEndpointUrl { get; set; }
    public string CodeInjectionUrl { get; set; }

    [Display(Name = "Webhook Secret")]
    public string WebhookSecret { get; set; }

    [Display(Name = "Webhook Subscription ID")]
    public string WebhookSubscriptionId { get; set; }

    [Display(Name = "Automatically Create Invoices")]
    public bool AutoCreateInvoices { get; set; } = true;

    [Display(Name = "Order Status After Payment")]
    public string OrderStatusAfterPayment { get; set; } = "FULFILLED";
}

public class SquarespaceWebhookNotification
{
    public string Id { get; set; } = string.Empty;
    public string WebsiteId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public SquarespaceOrderData Data { get; set; } = new();
}

public class SquarespaceOrderData
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public SquarespaceCustomer BillingAddress { get; set; } = new();
    public SquarespaceCustomer ShippingAddress { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "USD";
    public List<SquarespaceLineItem> LineItems { get; set; } = new();
    public string FulfillmentStatus { get; set; } = string.Empty;
}

public class SquarespaceCustomer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class SquarespaceLineItem
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPricePaid { get; set; }
}
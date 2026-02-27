using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.JumpSeller.ViewModels;

public class JumpSellerPaymentRequest
{
    [FromForm(Name = "x_account_id")] public string XAccountId { get; set; } = "";
    [FromForm(Name = "x_amount")] public string XAmount { get; set; } = "";
    [FromForm(Name = "x_currency")] public string XCurrency { get; set; } = "";
    [FromForm(Name = "x_reference")] public string XReference { get; set; } = "";
    [FromForm(Name = "x_url_callback")] public string XUrlCallback { get; set; } = "";
    [FromForm(Name = "x_url_complete")] public string XUrlComplete { get; set; } = "";
    [FromForm(Name = "x_url_cancel")] public string XUrlCancel { get; set; } = "";
    [FromForm(Name = "x_shop_name")] public string XShopName { get; set; } = "";
    [FromForm(Name = "x_shop_country")] public string XShopCountry { get; set; } = "";
    [FromForm(Name = "x_description")] public string XDescription { get; set; } = "";
    [FromForm(Name = "x_signature")] public string XSignature { get; set; } = "";
    [FromForm(Name = "x_customer_email")] public string XCustomerEmail { get; set; } = "";
    [FromForm(Name = "x_customer_first_name")] public string XCustomerFirstName { get; set; } = "";
    [FromForm(Name = "x_customer_last_name")] public string XCustomerLastName { get; set; } = "";
    [FromForm(Name = "x_customer_phone")] public string XCustomerPhone { get; set; } = "";
    [FromForm(Name = "x_customer_shipping_city")] public string XShippingCity { get; set; } = "";
    [FromForm(Name = "x_customer_shipping_country")] public string XShippingCountry { get; set; } = "";

    public Dictionary<string, string> ToDictionary()
    {
        var d = new Dictionary<string, string>();
        void Add(string k, string? v) { if (!string.IsNullOrEmpty(v)) d[k] = v; }
        Add("x_account_id", XAccountId);
        Add("x_amount", XAmount);
        Add("x_currency", XCurrency);
        Add("x_reference", XReference);
        Add("x_url_callback", XUrlCallback);
        Add("x_url_complete", XUrlComplete);
        Add("x_url_cancel", XUrlCancel);
        Add("x_shop_name", XShopName);
        Add("x_shop_country", XShopCountry);
        Add("x_description", XDescription);
        Add("x_signature", XSignature);
        Add("x_customer_email", XCustomerEmail);
        Add("x_customer_first_name", XCustomerFirstName);
        Add("x_customer_last_name", XCustomerLastName);
        Add("x_customer_phone", XCustomerPhone);
        Add("x_customer_shipping_city", XShippingCity);
        Add("x_customer_shipping_country", XShippingCountry);
        return d;
    }

}

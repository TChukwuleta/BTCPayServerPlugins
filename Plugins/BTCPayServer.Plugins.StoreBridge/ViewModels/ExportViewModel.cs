using System.Collections.Generic;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;
public class ExportViewModel
{
    public string StoreId { get; set; }
    public List<string> SelectedOptions { get; set; } = new List<string>();

    public static readonly List<string> AllOptions = new List<string>
    {
        "StoreSettings",
        "PaymentMethods",
        "Apps",
        "Webhooks",
        "EmailSettings",
        "CheckoutSettings",
        "Rates",
        "Policies"
    };
    public bool IsSelected(string option) => SelectedOptions?.Contains(option) ?? false;

    public static readonly Dictionary<string, (string Title, string Description)> OptionMetadata = new()
    {
        ["StoreSettings"] = ("Store Settings", "Name, website, default currency, speed policy, etc."),
        ["PaymentMethods"] = ("Payment Methods", "On-chain and Lightning Network configurations"),
        ["Apps"] = ("Apps", "Point of Sale, Crowdfund, and other app configurations"),
        ["Webhooks"] = ("Webhooks", "Webhook configurations and endpoints"),
        ["EmailSettings"] = ("Email Settings", "SMTP configuration and email templates"),
        ["CheckoutSettings"] = ("Checkout Settings", "Checkout appearance, forms, and behavior"),
        ["Rates"] = ("Rate Sources", "Exchange rate providers and preferences"),
        ["Policies"] = ("Policies", "Access policies and user permissions")
    };
}
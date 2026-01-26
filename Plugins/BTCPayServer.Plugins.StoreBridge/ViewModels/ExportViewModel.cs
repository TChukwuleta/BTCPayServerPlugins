using System.Collections.Generic;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;
public class ExportViewModel
{
    public string StoreId { get; set; }
    public List<string> SelectedOptions { get; set; } = new List<string>();

    public static readonly List<string> AllOptions = new List<string>
    {
        "BrandingSettings",
        "EmailSettings",
        "RateSettings",
        "CheckoutSettings",
        "Webhooks",
        "Roles",
        "Forms",
        "PaymentMethods",
        "Apps"
    };
    public bool IsSelected(string option) => SelectedOptions?.Contains(option) ?? false;

    public static readonly Dictionary<string, (string Title, string Description)> OptionMetadata = new()
    {
        ["BrandingSettings"] = ("Branding Settings", "Logo, CSS, brand colors and backend appearance"),
        ["EmailSettings"] = ("Email Settings", "Primary and fallback rate settings configuration"),
        ["RateSettings"] = ("Rate Settings", "Exchange rate providers and preferences"),
        ["CheckoutSettings"] = ("Checkout Settings", "Payment UI, language, timers, and user experience options"),
        ["Webhooks"] = ("Webhooks", "Webhook configurations and endpoints"),
        ["Roles"] = ("Roles & Permissions", "Store roles and user access permissions"),
        ["Forms"] = ("Forms", "Custom forms and their configurations"),
        ["PaymentMethods"] = ("Payment Methods", "On-chain and Lightning Network configurations"),
        ["Apps"] = ("Apps", "Point of Sale, Crowdfund, and other app configurations")
    };
}
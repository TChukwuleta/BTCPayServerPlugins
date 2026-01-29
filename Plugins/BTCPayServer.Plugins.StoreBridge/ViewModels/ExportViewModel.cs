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
        "PaymentMethods",
        "Webhooks",
        "Roles",
        "Forms",
        "Subscriptions"
    };
    public bool IsSelected(string option) => SelectedOptions?.Contains(option) ?? false;

    public static readonly Dictionary<string, (string Title, string Description)> OptionMetadata = new()
    {
        ["BrandingSettings"] = ("Branding Settings", "Logo, CSS, brand colors and backend appearance"),
        ["EmailSettings"] = ("Email Settings", "Settings details"),
        ["RateSettings"] = ("Rate Settings", "Primary and fallback rate settings configuration"),
        ["CheckoutSettings"] = ("Checkout Settings", "Payment UI, language, timers, and user experience options"),
        ["PaymentMethods"] = ("Wallet Settings", "On-chain payment method configurations (xpubs only, no private keys)"),
        ["Webhooks"] = ("Webhooks", "Webhook configurations and endpoints"),
        ["Roles"] = ("Roles & Permissions", "Store roles and access permissions"),
        ["Forms"] = ("Forms", "Custom forms and their configurations"),
        ["Subscriptions"] = ("Subscription Offerings & Plans", "Subscription offerings, plans, and features")
    };
}
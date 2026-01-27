using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class ImportViewModel
{
    public string StoreId { get; set; }
    public IFormFile ImportFile { get; set; }
    public List<string> SelectedOptions { get; set; } = new List<string>();
    public bool ShowPreview { get; set; }
    public string ExportedFrom { get; set; }
    public DateTime? ExportDate { get; set; }
    public string OriginalStoreName { get; set; }
    public int ExportVersion { get; set; }
    public List<string> AvailableOptions { get; set; } = new List<string>();
    public bool IsSelected(string option) => SelectedOptions?.Contains(option) ?? false;

    public bool IsAvailable(string option) => AvailableOptions?.Contains(option) ?? false;

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

    public static readonly Dictionary<string, (string Title, string Description)> OptionMetadata = new()
    {
        ["BrandingSettings"] = ("Branding Settings", "Logo, CSS, brand colors and backend appearance"),
        ["EmailSettings"] = ("Email Settings", "SMTP configuration and email templates"),
        ["RateSettings"] = ("Rate Settings", "Exchange rate providers and preferences"),
        ["CheckoutSettings"] = ("Checkout Settings", "Payment UI, language, timers, and user experience options"),
        ["Webhooks"] = ("Webhooks", "Webhook configurations and endpoints"),
        ["Roles"] = ("Roles & Permissions", "Store roles and user access permissions"),
        ["Forms"] = ("Forms", "Custom forms and their configurations"),
        ["PaymentMethods"] = ("Payment Methods", "On-chain and Lightning Network configurations"),
        ["Apps"] = ("Apps", "Point of Sale, Crowdfund, and other app configurations")
    };

    public string FormattedExportDate => ExportDate?.ToString("MMMM dd, yyyy 'at' HH:mm 'UTC'") ?? "Unknown";

    public int SelectedCount => SelectedOptions?.Count ?? 0;

    public int AvailableCount => AvailableOptions?.Count ?? 0;
}
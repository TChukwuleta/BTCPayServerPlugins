using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class StoreExportData
{
    public int Version { get; set; } = 1;
    public string SelectedOptions { get; set; }
    public DateTime ExportDate { get; set; }
    public string ExportedFrom { get; set; } = string.Empty;
    public List<AppExport> Apps { get; set; }
    public List<WebhookExport> Webhooks { get; set; }
    public List<RoleExport> Roles { get; set; }
    public List<FormExport> Forms { get; set; }
    public List<PaymentMethodExportData> PaymentMethods { get; set; }
    public List<SubscriptionOfferingExportData> SubscriptionOfferings { get; set; }
    public StoreBridgeData Store { get; set; } = new();
}

public class AppExport
{
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string AppType { get; set; }
    public string SettingsJson { get; set; }
}
public class WebhookExport
{
    public string BlobJson { get; set; }
    public string Blob2Json { get; set; }
}
public class RoleExport
{
    public string Role { get; set; }
    public List<string> Permissions { get; set; }
}
public class FormExport
{
    public bool Public { get; set; }
    public string Name { get; set; }
    public string Config { get; set; }
}
public class PaymentMethodExportData
{
    public string PaymentMethodId { get; set; } // e.g., "BTC", "BTC-LightningNetwork"
    public string CryptoCode { get; set; } // e.g., "BTC"
    public string PaymentType { get; set; } // "CHAIN" or "LN" or "LNURL"
    public string AccountDerivation { get; set; } // xpub/ypub/zpub
    public string AccountOriginal { get; set; }
    public string Label { get; set; }
    public List<AccountKeySettingsData> AccountKeySettings { get; set; }
    public bool IsHotWallet { get; set; }
    public string Source { get; set; }
}
public class AccountKeySettingsData
{
    public string RootFingerprint { get; set; }
    public string AccountKeyPath { get; set; }
    public string AccountKey { get; set; }
}
public class SubscriptionOfferingExportData
{
    public string AppName { get; set; }
    public string SuccessRedirectUrl { get; set; }
    public List<OfferingFeatureData> Features { get; set; }
    public List<SubscriptionPlanExportData> Plans { get; set; }
    public string Metadata { get; set; }
    public int DefaultPaymentRemindersDays { get; set; }
}
public class OfferingFeatureData
{
    public string CustomId { get; set; }
    public string Description { get; set; }
}
public class SubscriptionPlanExportData
{
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; }
    public string RecurringType { get; set; } // "Monthly", "Quarterly", "Yearly", "Lifetime"
    public int TrialDays { get; set; }
    public int GracePeriodDays { get; set; }
    public bool OptimisticActivation { get; set; }
    public bool Renewable { get; set; }
    public string Status { get; set; } // "Active" or "Retired"
    public List<string> FeatureIds { get; set; }
    public string Metadata { get; set; }
}
public class StoreBridgeData
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string StoreBlob { get; set; }
    public string SpeedPolicy { get; set; }
    public string DerivationStrategies { get; set; }
}
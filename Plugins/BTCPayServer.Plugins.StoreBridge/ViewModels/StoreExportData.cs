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
    public List<SubscriptionPlanExportData> SubscriptionPlans { get; set; }
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

    // For on-chain (DerivationSchemeSettings)
    public string AccountDerivation { get; set; } // xpub/ypub/zpub
    public string AccountOriginal { get; set; }
    public string Label { get; set; }
    public List<AccountKeySettingsData> AccountKeySettings { get; set; }
    public bool IsHotWallet { get; set; }
    public string Source { get; set; }

    // For Lightning (LightningPaymentMethodConfig)
    public string LightningConnectionType { get; set; } // "Internal", "LndGRPC", "CLightning", etc.
    public string InternalNodeRef { get; set; }

    // Common settings
    public JObject? AdditionalData { get; set; }
}
public class AccountKeySettingsData
{
    public string RootFingerprint { get; set; }
    public string AccountKeyPath { get; set; }
    public string AccountKey { get; set; }
}
public class SubscriptionPlanExportData
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; }
    public string BillingPeriod { get; set; } // "Monthly", "Yearly", etc.
    public int? TrialPeriodDays { get; set; }
    public bool IsActive { get; set; }
    public JObject AdditionalData { get; set; }
}
public class StoreBridgeData
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string StoreBlob { get; set; }
    public string SpeedPolicy { get; set; }
    public string DerivationStrategies { get; set; }
}
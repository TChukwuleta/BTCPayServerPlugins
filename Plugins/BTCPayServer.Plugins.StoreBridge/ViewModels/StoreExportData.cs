using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class StoreExportData
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAt { get; set; }
    public string ExportedFrom { get; set; } = string.Empty;
    public StoreData Store { get; set; } = new();
    public List<WalletData> Wallets { get; set; } = new();
    public List<PaymentMethodData> PaymentMethods { get; set; } = new();
    public List<WebhookData> Webhooks { get; set; } = new();
    public List<StoreUserData> Users { get; set; } = new();
    public List<AppData> Apps { get; set; } = new();
    public Dictionary<string, object> PluginData { get; set; } = new();
}


/// <summary>
/// Core store configuration
/// </summary>
public class StoreData
{
    public string Id { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? StoreWebsite { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public int SpeedPolicy { get; set; }
    public string? NetworkFeeMode { get; set; }
    public decimal? Spread { get; set; }
    public Dictionary<string, decimal>? RateRules { get; set; }
    public bool PayJoinEnabled { get; set; }
    public bool AnyoneCanCreateInvoice { get; set; }
    public bool RequiresRefundEmail { get; set; }
    public string? CustomLogo { get; set; }
    public string? CustomCSS { get; set; }
    public string? DefaultLang { get; set; }
    public bool InvoiceExpiration { get; set; }
    public int InvoiceExpirationMinutes { get; set; }
    public int MonitoringExpiration { get; set; }
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}

/// <summary>
/// Wallet configuration (xpub only, never private keys)
/// </summary>
public class WalletData
{
    public string CryptoCode { get; set; } = string.Empty;
    public string DerivationScheme { get; set; } = string.Empty;
    public string? Label { get; set; }
    public bool Enabled { get; set; }
    public string? AccountKeyPath { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public WalletType Type { get; set; }
}

public enum WalletType
{
    OnChain,
    Lightning
}

/// <summary>
/// Payment method configuration
/// </summary>
public class PaymentMethodData
{
    public string CryptoCode { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Webhook configuration
/// </summary>
public class WebhookData
{
    public bool Enabled { get; set; }
    public bool AutomaticRedelivery { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<string> AuthorizedEvents { get; set; } = new();
    public string? Secret { get; set; }
}

/// <summary>
/// Store user and role mapping
/// </summary>
public class StoreUserData
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// App configuration (PoS, Crowdfund, etc.)
/// </summary>
public class AppData
{
    public string AppType { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Import options
/// </summary>
public class StoreImportOptions
{
    public bool ImportWallets { get; set; } = true;
    public bool ImportPaymentMethods { get; set; } = true;
    public bool ImportWebhooks { get; set; } = true;
    public bool ImportUsers { get; set; } = false; // Default false for security
    public bool ImportApps { get; set; } = true;
    public bool OverwriteExisting { get; set; } = false;
    public string? NewStoreId { get; set; } // If null, keeps original or generates new
    public string? NewStoreName { get; set; } // If null, keeps original
}

/// <summary>
/// Import result
/// </summary>
public class StoreImportResult
{
    public bool Success { get; set; }
    public string? NewStoreId { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public ImportStatistics Statistics { get; set; } = new();
}

public class ImportStatistics
{
    public int WalletsImported { get; set; }
    public int PaymentMethodsImported { get; set; }
    public int WebhooksImported { get; set; }
    public int UsersImported { get; set; }
    public int AppsImported { get; set; }
}

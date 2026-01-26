using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class StoreExportData
{
    public int Version { get; set; } = 1;
    public DateTime ExportDate { get; set; }
    public string ExportedFrom { get; set; } = string.Empty;
    public List<PaymentMethodExport> PaymentMethods { get; set; }
    public List<AppExport> Apps { get; set; }
    public List<WebhookExport> Webhooks { get; set; }
    public List<RoleExport> Roles { get; set; }
    public List<FormExport> Forms { get; set; }
    public StoreBridgeData Store { get; set; } = new();
}

public class PaymentMethodExport
{
    public string PaymentMethodId { get; set; }
    public string ConfigJson { get; set; }
}
public class AppExport
{
    public string AppId { get; set; }
    public string AppName { get; set; }
    public string AppType { get; set; }
    public DateTimeOffset Created { get; set; }
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
public class StoreBridgeData
{
    public string Id { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = "USD";
    public string StoreBlob { get; set; }
    public string SpeedPolicy { get; set; }
    public string DerivationStrategies { get; set; }
    public string StoreWebsite { get; set; }
    public decimal Spread { get; set; }
    public string DefaultLang { get; set; }
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

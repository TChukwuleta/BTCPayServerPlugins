using System;
using System.Collections.Generic;

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
public class StoreBridgeData
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string StoreBlob { get; set; }
    public string SpeedPolicy { get; set; }
    public string DerivationStrategies { get; set; }
}
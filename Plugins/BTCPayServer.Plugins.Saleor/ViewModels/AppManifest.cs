using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class AppManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("about")]
    public string About { get; set; }

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; }

    [JsonPropertyName("appUrl")]
    public string AppUrl { get; set; } 

    [JsonPropertyName("tokenTargetUrl")]
    public string TokenTargetUrl { get; set; }

    [JsonPropertyName("webhooks")]
    public WebhookManifest[] Webhooks { get; set; }

    [JsonPropertyName("extensions")]
    public object[] Extensions { get; set; }

    [JsonPropertyName("brand")]
    public BrandManifest? Brand { get; set; }
}

public class BrandManifest
{
    [JsonPropertyName("logo")]
    public LogoManifest? Logo { get; set; }
}

public class LogoManifest
{
    [JsonPropertyName("default")]
    public string Default { get; set; }
}

public class WebhookManifest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } 

    [JsonProperty("asyncEvents", NullValueHandling = NullValueHandling.Ignore)]
    public string[]? AsyncEvents { get; set; }

    [JsonProperty("syncEvents", NullValueHandling = NullValueHandling.Ignore)]
    public string[]? SyncEvents { get; set; }

    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("targetUrl")]
    public string TargetUrl { get; set; } 

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class SaleorAppPageViewModel : BaseSaleorPublicViewModel
{
    public AplEntry ConnectedInstance { get; set; }
}

public class SaleorDashboardViewModel : SaleorAppPageViewModel
{
    public string ManifestUrl { get; set; }
}

public class ConnectedInstance
{
    public string SaleorApiUrl { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
}

public class DisconnectRequest
{
    public string SaleorApiUrl { get; set; }
}
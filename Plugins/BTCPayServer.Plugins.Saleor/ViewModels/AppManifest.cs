using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class AppManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "saleor.app.btcpay";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "BTCPay Server";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "BTCPay Server";

    [JsonPropertyName("about")]
    public string About { get; set; } = "Accept Bitcoin payments via BTCPay Server";

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = ["MANAGE_ORDERS", "HANDLE_PAYMENTS"];

    [JsonPropertyName("appUrl")]
    public string AppUrl { get; set; } = "";

    [JsonPropertyName("tokenTargetUrl")]
    public string TokenTargetUrl { get; set; } = "";

    [JsonPropertyName("webhooks")]
    public WebhookManifest[] Webhooks { get; set; } = [];

    [JsonPropertyName("extensions")]
    public object[] Extensions { get; set; } = [];

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
    public string Default { get; set; } = "";
}

public class WebhookManifest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("asyncEvents")]
    public string[]? AsyncEvents { get; set; }

    [JsonPropertyName("syncEvents")]
    public string[]? SyncEvents { get; set; }

    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("targetUrl")]
    public string TargetUrl { get; set; } = "";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class SaleorAppPageViewModel : BaseSaleorPublicViewModel
{
    public AplEntry? ConnectedInstance { get; set; }
}

public class ConnectedInstance
{
    public string SaleorApiUrl { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; }
}

public class DisconnectRequest
{
    public string SaleorApiUrl { get; set; } = "";
}
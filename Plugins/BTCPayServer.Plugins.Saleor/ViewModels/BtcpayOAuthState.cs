using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class BtcpayOAuthState
{
    [JsonPropertyName("instanceUrl")]
    public string InstanceUrl { get; set; } = "";

    [JsonPropertyName("storeId")]
    public string StoreId { get; set; } = "";

    [JsonPropertyName("storefrontUrl")]
    public string StorefrontUrl { get; set; } = "";

    [JsonPropertyName("saleorApiUrl")]
    public string SaleorApiUrl { get; set; } = "";
}
public class BtcpayConfig
{
    public string InstanceUrl { get; set; } = "";
    public string StoreId { get; set; } = "";
    public string StorefrontUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
public class BtcpayCallbackBody
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = [];

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";
}
using System;
using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class AplEntry
{
    public string SaleorApiUrl { get; set; } = "";
    public string Token { get; set; } = "";
    public string AppId { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}
public class SaleorRegisterRequest
{
    [JsonPropertyName("auth_token")]
    public string AuthToken { get; set; } = "";
}

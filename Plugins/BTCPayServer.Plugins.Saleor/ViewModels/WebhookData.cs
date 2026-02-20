using System.Text.Json.Serialization;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

internal class WebhookData
{
}

public class TransactionInitializeSessionPayload
{
    [JsonPropertyName("transaction")]
    public TransactionData Transaction { get; set; } = new();

    [JsonPropertyName("action")]
    public ActionData Action { get; set; } = new();

    [JsonPropertyName("sourceObject")]
    public SourceObject? SourceObject { get; set; }

    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; set; }
}

public class TransactionProcessSessionPayload
{
    [JsonPropertyName("transaction")]
    public TransactionData Transaction { get; set; } = new();

    [JsonPropertyName("action")]
    public ActionData Action { get; set; } = new();

    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; set; }
}

public class TransactionWebhookResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("pspReference")]
    public string PspReference { get; set; } = "";

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("externalUrl")]
    public string? ExternalUrl { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class TransactionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("pspReference")]
    public string? PspReference { get; set; }
}

public class ActionData
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = "";
}

public class SourceObject
{
    [JsonPropertyName("channel")]
    public ChannelData? Channel { get; set; }

    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; set; }
}

public class ChannelData
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

internal class GraphqlResponse
{
}

public class GraphQLRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = "";

    [JsonPropertyName("variables")]
    public object? Variables { get; set; }
}

public class GraphQLResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public GraphQLError[]? Errors { get; set; }
}

public class GraphQLError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class AppMetadataResponse
{
    [JsonPropertyName("app")]
    public AppData? App { get; set; }
}

public class AppData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("privateMetadata")]
    public MetadataEntry[] PrivateMetadata { get; set; } = [];
}

public class MetadataEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public class UpdateMetadataResponse
{
    [JsonPropertyName("updatePrivateMetadata")]
    public UpdateMetadataResult? UpdatePrivateMetadata { get; set; }
}

public class UpdateMetadataResult
{
    [JsonPropertyName("errors")]
    public MetadataError[] Errors { get; set; } = [];
}

public class MetadataError
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class BtcpayAuthRequest
{
    public string InstanceUrl { get; set; } = "";
    public string StoreId { get; set; } = "";
    public string StorefrontUrl { get; set; } = "";
    public string SaleorApiUrl { get; set; } = "";
}
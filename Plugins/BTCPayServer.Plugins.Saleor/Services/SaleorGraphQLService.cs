using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Saleor.ViewModels;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorGraphQLService
{
    private readonly HttpClient _http;
    private readonly ILogger<SaleorGraphQLService> _logger;

    private const string GetAppMetadataQuery = """
        query GetAppMetadata {
            app {
                id
                privateMetadata {
                    key
                    value
                }
            }
        }
        """;

    private const string UpdatePrivateMetadataMutation = """
        mutation UpdatePrivateMetadata($id: ID!, $input: [MetadataInput!]!) {
            updatePrivateMetadata(id: $id, input: $input) {
                errors { field message }
                item {
                    privateMetadata { key value }
                }
            }
        }
        """;

    private const string DeletePrivateMetadataMutation = """
        mutation DeletePrivateMetadata($id: ID!, $keys: [String!]!) {
            deletePrivateMetadata(id: $id, keys: $keys) {
                errors { field message }
            }
        }
        """;

    private const string TransactionEventReportMutation = """
        mutation TransactionEventReport(
            $transactionId: ID!
            $amount: PositiveDecimal!
            $availableActions: [TransactionActionEnum!]!
            $externalUrl: String!
            $message: String
            $pspReference: String!
            $time: DateTime
            $type: TransactionEventTypeEnum!
        ) {
            transactionEventReport(
                transactionId: $transactionId
                amount: $amount
                availableActions: $availableActions
                externalUrl: $externalUrl
                message: $message
                pspReference: $pspReference
                time: $time
                type: $type
            ) {
                errors { field message code }
                alreadyProcessed
                transaction { id }
            }
        }
        """;

    public SaleorGraphQLService(HttpClient http, ILogger<SaleorGraphQLService> logger)
    {
        _http = http;
        _logger = logger;
    }

    private async Task<T?> ExecuteAsync<T>(string saleorApiUrl, string token, string query, object? variables = null)
    {
        var request = new GraphQLRequest { Query = query, Variables = variables };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _http.PostAsync(saleorApiUrl, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GraphQLResponse<T>>(responseJson);

        if (result?.Errors?.Length > 0)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("GraphQL errors: {Errors}", errors);
            throw new Exception($"GraphQL errors: {errors}");
        }
        return result is null ? default : result.Data;
    }

    private async Task<AppData?> GetAppAsync(string saleorApiUrl, string token)
    {
        var result = await ExecuteAsync<AppMetadataResponse>(saleorApiUrl, token, GetAppMetadataQuery);
        return result?.App;
    }

    public async Task<BtcpayConfig?> GetConfigAsync(string saleorApiUrl, string token)
    {
        var app = await GetAppAsync(saleorApiUrl, token);
        if (app is null) return null;

        var entry = app.PrivateMetadata.FirstOrDefault(m => m.Key == "btcpay.config");
        if (entry is null) return null;

        return JsonSerializer.Deserialize<BtcpayConfig>(entry.Value);
    }

    public async Task SetConfigAsync(string saleorApiUrl, string token, BtcpayConfig config)
    {
        var app = await GetAppAsync(saleorApiUrl, token);
        if (app is null) throw new Exception("Could not fetch app from Saleor");

        var result = await ExecuteAsync<UpdateMetadataResponse>(
            saleorApiUrl, token, UpdatePrivateMetadataMutation,
            new
            {
                id = app.Id,
                input = new[] { new { key = "btcpay.config", value = JsonSerializer.Serialize(config) } }
            });

        var errors = result?.UpdatePrivateMetadata?.Errors ?? [];
        if (errors.Length > 0)
            throw new Exception($"Metadata update failed: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    public async Task DeleteConfigAsync(string saleorApiUrl, string token)
    {
        var app = await GetAppAsync(saleorApiUrl, token);
        if (app is null) throw new Exception("Could not fetch app from Saleor");

        await ExecuteAsync<object>(saleorApiUrl, token, DeletePrivateMetadataMutation,
            new { id = app.Id, keys = new[] { "btcpay.config" } });
    }

    // ─── Transaction event reporting (used by BTCPay webhook handler) ──────────

    public async Task ReportTransactionEventAsync(
        string saleorApiUrl,
        string token,
        string transactionId,
        decimal amount,
        string type,           // e.g. "CHARGE_SUCCESS", "CHARGE_FAILURE"
        string pspReference,
        string externalUrl,
        string? message = null)
    {
        var availableActions = type == "CHARGE_SUCCESS"
            ? new[] { "REFUND" }
            : Array.Empty<string>();

        await ExecuteAsync<object>(saleorApiUrl, token, TransactionEventReportMutation, new
        {
            transactionId,
            amount,
            availableActions,
            externalUrl,
            message,
            pspReference,
            time = DateTime.UtcNow.ToString("o"),
            type
        });

        _logger.LogInformation("Reported {Type} event for transaction {Id}", type, transactionId);
    }
}

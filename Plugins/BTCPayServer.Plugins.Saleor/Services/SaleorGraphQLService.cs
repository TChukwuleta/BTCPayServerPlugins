using System;
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

    private const string TransactionEventReportMutation = """
        mutation TransactionEventReport(
            $id: ID!
            $amount: PositiveDecimal!
            $availableActions: [TransactionActionEnum!]!
            $externalUrl: String!
            $message: String
            $pspReference: String!
            $time: DateTime
            $type: TransactionEventTypeEnum!
        ) {
            transactionEventReport(
                id: $id
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
        var responseJson = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var result = JsonSerializer.Deserialize<GraphQLResponse<T>>(responseJson);
        if (result?.Errors?.Length > 0)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("GraphQL errors: {Errors}", errors);
            throw new Exception($"GraphQL errors: {errors}");
        }
        return result is null ? default : result.Data;
    }

    public async Task ReportTransactionEventAsync(string saleorApiUrl, string token, string transactionId, decimal amount,
        string type, string pspReference, string externalUrl, string? message = null)
    {
        // Uncomment when there is refund implementation
        // var availableActions = type == "CHARGE_SUCCESS" ? new[] { "REFUND" } : Array.Empty<string>();
        var availableActions = Array.Empty<string>();
        await ExecuteAsync<object>(saleorApiUrl, token, TransactionEventReportMutation, new
        {
            id = transactionId, amount, availableActions, externalUrl, message, pspReference,
            time = DateTime.UtcNow.ToString("o"), type
        });
        _logger.LogInformation("Reported {Type} event for transaction {Id}", type, transactionId);
    }
}

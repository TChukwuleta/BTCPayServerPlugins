using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class MavapayApiClientService
{
    private readonly HttpClient _httpClient;
    public readonly string ApiUrl = "https://staging.api.mavapay.co/api/v1";
    public MavapayApiClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory?.CreateClient(nameof(MavapayApiClientService)) ?? new HttpClient();
    }

    // Quote
    public async Task<CreateQuoteResponseVm> CreateQuote(CreateQuoteRequestVm requestModel, string apiKey)
    {
        var postJson = JsonConvert.SerializeObject(requestModel);
        var req = CreateRequest(HttpMethod.Post, "quote");
        req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<CreateQuoteResponseVm>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        return responseModel.data;
    }

    // Webhook
    public async Task<bool> RegisterWebhook(string apiKey, string url, string secret)
    {
        var body = new { url, secret };
        var postJson = JsonConvert.SerializeObject(body);
        var req = CreateRequest(HttpMethod.Post, "webhook/register");
        req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<CreateWebhookResponseVm>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        return responseModel.status.ToLower().Trim() == "success";
    }

    public async Task<bool> UpdateWebhook(string apiKey, string url, string secret)
    {
        var body = new { url, secret };
        var postJson = JsonConvert.SerializeObject(body);
        var req = CreateRequest(HttpMethod.Put, "webhook");
        req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<CreateWebhookResponseVm>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        return responseModel.status.ToLower().Trim() == "success";
    }


    // Transaction status
    public async Task<TransactionResponseVm> GetTransactionAsync(string apiKey, string id = null, string orderId = null, string hash = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(id))
            queryParams.Add($"id={Uri.EscapeDataString(id)}");

        if (!string.IsNullOrWhiteSpace(orderId))
            queryParams.Add($"orderId={Uri.EscapeDataString(orderId)}");

        if (!string.IsNullOrWhiteSpace(hash))
            queryParams.Add($"hash={Uri.EscapeDataString(hash)}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var req = CreateRequest(HttpMethod.Get, $"transaction/{queryString}");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<TransactionResponseVm>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        return responseModel.data;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl) => new HttpRequestMessage(method, $"{ApiUrl}/{relativeUrl}");

    private async Task<string> SendRequest(HttpRequestMessage req, string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        Console.WriteLine(apiKey);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        var requestBody = await req.Content.ReadAsStringAsync();
        Console.WriteLine(JsonConvert.SerializeObject(requestBody));
        Console.WriteLine(responseContent);
        return responseContent;
    }
}

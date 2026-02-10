using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.SquareSpace.Data;
using BTCPayServer.Plugins.SquareSpace.ViewModels;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.SquareSpace.Services;

public class SquarespaceService
{
    private readonly HttpClient _httpClient;
    private const string BaseApiUrl = "https://api.squarespace.com/1.0";
    public SquarespaceService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory?.CreateClient(nameof(SquarespaceService)) ?? new HttpClient();
    }

    public async Task<WebhookSubscriptionResponse> CreateWebhookSubscription(string oauthToken, string endpointUrl)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Post, "webhook_subscriptions");
            var payload = new
            {
                endpointUrl,
                topics = new[] { "order.create", "order.update" }
            };
            req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await SendRequest(req, oauthToken);

            if (!response.Success) return null;

            var result = JsonConvert.DeserializeObject<dynamic>(response.Message);
            return new WebhookSubscriptionResponse
            {
                SubscriptionId = result?.id?.ToString(),
                Secret = result?.secret?.ToString()
            };
        }
        catch (Exception) { return null; }
    }

    public async Task DeleteWebhookSubscription(string oauthToken, string subscriptionId)
    {
        var req = CreateRequest(HttpMethod.Delete, $"webhook_subscriptions/{subscriptionId}");
        var response = await SendRequest(req, oauthToken);
    }

    public async Task<SquarespaceOrderData> GetOrder(string oauthToken, string orderId)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Get, $"commerce/orders/{orderId}");
            var response = await SendRequest(req, oauthToken);

            if (!response.Success) return null;

            var order = JsonConvert.DeserializeObject<SquarespaceOrderData>(response.Message);
            return order;
        }
        catch (Exception){ return null; }
    }

    public async Task<GenericResponse> FulfillOrder(string oauthToken, string orderId)
    {
        try
        {
            var req = CreateRequest(HttpMethod.Post, $"commerce/orders/{orderId}/fulfillments");
            var payload = new { shouldSendNotification = true };
            req.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return await SendRequest(req, oauthToken);
        }
        catch (Exception) { return null; }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        return new HttpRequestMessage(method, $"{BaseApiUrl}/{relativeUrl}");
    }

    private async Task<GenericResponse> SendRequest(HttpRequestMessage req, string oauthToken)
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer-Squarespace-Plugin/1.0");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        return new GenericResponse { Message = responseContent, Success = response.IsSuccessStatusCode };
    }

    private bool VerifyWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return signature.Equals(computedSignature, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

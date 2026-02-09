using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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

            if (string.IsNullOrEmpty(response))
                return new WebhookSubscriptionResponse { Success = false, Error = "Failed to create webhook" };

            var result = JsonConvert.DeserializeObject<dynamic>(response);
            return new WebhookSubscriptionResponse
            {
                Success = true,
                SubscriptionId = result?.id?.ToString(),
                Secret = result?.secret?.ToString()
            };
        }
        catch (Exception ex)
        {
            return new WebhookSubscriptionResponse { Success = false, Error = ex.Message };
        }
    }

    /*public async Task<string> DeleteWebhookSubscription(string oauthToken, string subscriptionId)
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

            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{BaseApiUrl}/webhook_subscriptions/{subscriptionId}");
            request.Headers.Add("Authorization", $"Bearer {oauthToken}");
            request.Headers.Add("User-Agent", "BTCPayServer-Squarespace-Plugin/1.0");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Failed to delete webhook: {response.StatusCode} - {errorBody}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }*/

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        return new HttpRequestMessage(method, $"{BaseApiUrl}/{relativeUrl}");
    }

    private async Task<string> SendRequest(HttpRequestMessage req, string oauthToken)
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer-Squarespace-Plugin/1.0");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        return responseContent;
    }
}

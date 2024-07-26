using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceService
{
    private readonly string BTCPAY_SCRIPT_NAME = "btcpay-checkout";
    private readonly HttpClient _client;
    private readonly ILogger<BigCommerceService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public BigCommerceService(HttpClient client, ILogger<BigCommerceService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _client = client;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(bool success, string content)> InstallApplication(InstallBigCommerceApplicationRequestModel requestModel)
    {
        string result = string.Empty;
        using (var client = new HttpClient())
        {
            var requestUri = "https://login.bigcommerce.com/oauth2/token";
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string, string>("client_id", requestModel.ClientId),
                    new KeyValuePair<string, string>("client_secret", requestModel.ClientSecret),
                    new KeyValuePair<string, string>("code", requestModel.Code),
                    new KeyValuePair<string, string>("scope", requestModel.Scope),
                    new KeyValuePair<string, string>("grant_type", requestModel.GrantType),
                    new KeyValuePair<string, string>("redirect_uri", requestModel.RedirectUrl),
                    new KeyValuePair<string, string>("context", requestModel.Context),
            });
            var response = await client.PostAsync(requestUri, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                return (false, $"An error occurred while installing a big commerce application: {errorResponse}");
            }
            result = await response.Content.ReadAsStringAsync();
        }
        return (true, result);
    }

    public async Task<CreateCheckoutScriptResponse> SetCheckoutScriptAsync(string storeHash, string jsFilePath, string accessToken)
    {
        try
        {
            var payload = new
            {
                name = BTCPAY_SCRIPT_NAME,
                description = "Adds BTCPay Javascript to the checkout page.",
                src = $"{jsFilePath}?bcid={storeHash.Replace("stores/", string.Empty)}",
                auto_uninstall = true,
                load_method = "default",
                location = "footer",
                visibility = "checkout",
                kind = "src",
                consent_category = "essential",
                enabled = true,
            };
            var result = await MakeBigCommerceAPICallAsync(HttpMethod.Post, "v3/content/scripts", storeHash, payload, null, accessToken);
            return JsonConvert.DeserializeObject<CreateCheckoutScriptResponse>(await result.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Error setting file via BC API", ex);
        }
    }

    public async Task<bool> ConfirmOrderExistAsync(int orderId, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Get, $"v2/orders/{orderId}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
        {
            _logger.LogError($"Unable to confirm order... Status code: {result.StatusCode.ToString()}... Error content: {result.Content.ToString()}");
            return false;
        }
        return true;
    }

    public async Task UpdateOrderStatusAsync(int orderId, BigCommerceOrderState status, string storeHash, string accessToken)
    {
        var data = new { status_id = (int)status };
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Put, $"v2/orders/{orderId}", storeHash, data, null, accessToken);
        if (!result.IsSuccessStatusCode)
        {
            _logger.LogError($"Unable to update order status... Status code: {result.StatusCode.ToString()}... Error content: {result.Content.ToString()}");
        }
    }

    public async Task<CreateBigCommerceOrderResponse> CheckoutOrderAsync(string storeHash, string checkoutId, string accessToken)
    {
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Post, $"v3/checkouts/{checkoutId}/orders", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
        {
            return null;
        }
        return JsonConvert.DeserializeObject<CreateBigCommerceOrderResponse>(await result.Content.ReadAsStringAsync());
    }


    public async Task<GetCheckoutScriptResponse> GetCheckoutScriptAsync(string scriptUuid, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Get, $"v3/content/scripts/{scriptUuid}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
        {
            return null;
        } 
        return JsonConvert.DeserializeObject<GetCheckoutScriptResponse>(await result.Content.ReadAsStringAsync());
    }

    public async Task<DeleteCheckoutScriptResponse> DeleteCheckoutScriptAsync(string scriptUuid, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Delete, $"v3/content/scripts/{scriptUuid}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
        {
            return null;
        }
        return JsonConvert.DeserializeObject<DeleteCheckoutScriptResponse>(await result.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> MakeBigCommerceAPICallAsync(HttpMethod method, string endpoint, string storeHash, object data = null, string clientId = null, string accessToken = null)
    {
        var request = new HttpRequestMessage(method, $"https://api.bigcommerce.com/{storeHash}/{endpoint}");
        if (!string.IsNullOrEmpty(clientId))
        {
            request.Headers.Add("X-Auth-Client", clientId);
        }
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Add("X-Auth-Token", accessToken);
        }
        request.Headers.Add("Accept", "application/json");
        if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
        }
        try
        {
            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request exception: {e.Message}");
            Console.WriteLine($"Request status code: {e.StatusCode}");
            throw;
        }
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        return baseUrl;
    }
}

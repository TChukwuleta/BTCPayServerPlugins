﻿using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
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
    private const string AuthenticationUrl = "https://login.bigcommerce.com/oauth2/token";
    private readonly HttpClient _client;
    public BigCommerceService(HttpClient client)
    {
        _client = client;
    }


    public async Task<GenericResponse> InstallApplication(InstallBigCommerceApplicationRequestModel requestModel)
    {
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
        var response = await _client.PostAsync(AuthenticationUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadAsStringAsync();
            return new GenericResponse
            {
                Success = false,
                Content = $"An error occurred while installing a BigCommerce application: {errorResponse}"
            };
        }
        return new GenericResponse
        {
            Success = true,
            Content = await response.Content.ReadAsStringAsync()
        };
    }

    public async Task<CreateCheckoutScriptResponse> SetCheckoutScriptAsync(string storeHash, string jsFilePath, string accessToken)
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

    public async Task<bool> ConfirmOrderExistAsync(long orderId, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICallAsync(HttpMethod.Get, $"v2/orders/{orderId}", storeHash, null, null, accessToken);
        return result.IsSuccessStatusCode;
    }

    public async Task UpdateOrderStatusAsync(long orderId, BigCommerceOrderState status, string storeHash, string accessToken)
    {
        var data = new { status_id = (int)status };
        await MakeBigCommerceAPICallAsync(HttpMethod.Put, $"v2/orders/{orderId}", storeHash, data, null, accessToken);
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
            Console.WriteLine($"An error occurred: Request status code: {e.StatusCode}...  Exception message: {e.Message}");
            throw;
        }
    }
}

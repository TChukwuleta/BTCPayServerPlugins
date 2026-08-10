using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceService(HttpClient client)
{
    private readonly string BTCPAY_SCRIPT_NAME = "btcpay-checkout";
    private const string AuthenticationUrl = "https://login.bigcommerce.com/oauth2/token";

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
        var response = await client.PostAsync(AuthenticationUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return new GenericResponse
            {
                Success = false,
                Content = $"An error occurred while installing a BigCommerce application: {body}"
            };
        }
        return new GenericResponse { Success = true, Content = body };
    }


    public async Task<CreateCheckoutScriptResponse> SetCheckoutScript(string storeHash, string jsFilePath, string accessToken)
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
        var result = await MakeBigCommerceAPICall(HttpMethod.Post, "v3/content/scripts", storeHash, payload, null, accessToken);
        if (!result.IsSuccessStatusCode)
            return null;

        return await DeserializeOrLog<CreateCheckoutScriptResponse>(result, "checkout script creation");
    }

    public async Task<bool> ConfirmOrderExist(long orderId, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICall(HttpMethod.Get, $"v2/orders/{orderId}", storeHash, null, null, accessToken);
        return result.IsSuccessStatusCode;
    }

    public async Task UpdateOrderStatus(long orderId, BigCommerceOrderState status, string storeHash, string accessToken)
    {
        var data = new { status_id = (int)status };
        await MakeBigCommerceAPICall(HttpMethod.Put, $"v2/orders/{orderId}", storeHash, data, null, accessToken);
    }

    public async Task<BigCommerceOrderDetails> GetOrder(long orderId, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICall(HttpMethod.Get, $"v2/orders/{orderId}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
            return null;

        var order = await DeserializeOrLog<BigCommerceOrderDetails>(result, "Get order");
        if (order is null || string.IsNullOrEmpty(order.total_inc_tax) || string.IsNullOrEmpty(order.currency_code))
        {
            Console.WriteLine(
                "BigCommerce order {OrderId} response did not contain the expected total_inc_tax/currency_code " +
                "fields - check the raw response logged above and adjust BigCommerceOrderDetails to match.", orderId);
        }
        return order;
    }

    public async Task<CreateBigCommerceOrderResponse> CheckoutOrder(string storeHash, string checkoutId, string accessToken)
    {
        var result = await MakeBigCommerceAPICall(HttpMethod.Post, $"v3/checkouts/{checkoutId}/orders", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
            return null;

        return await DeserializeOrLog<CreateBigCommerceOrderResponse>(result, $"checkout {checkoutId} order creation");
    }

    public async Task<GetCheckoutScriptResponse> GetCheckoutScript(string scriptUuid, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICall(HttpMethod.Get, $"v3/content/scripts/{scriptUuid}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode)
            return null;

        return await DeserializeOrLog<GetCheckoutScriptResponse>(result, $"checkout script {scriptUuid} lookup");
    }

    public async Task<DeleteCheckoutScriptResponse> DeleteCheckoutScript(string scriptUuid, string storeHash, string accessToken)
    {
        var result = await MakeBigCommerceAPICall(HttpMethod.Delete, $"v3/content/scripts/{scriptUuid}", storeHash, null, null, accessToken);
        if (!result.IsSuccessStatusCode) 
            return null;

        return await DeserializeOrLog<DeleteCheckoutScriptResponse>(result, $"checkout script {scriptUuid} deletion");
    }

    private async Task<HttpResponseMessage> MakeBigCommerceAPICall(HttpMethod method, string endpoint, string storeHash, object data = null, string clientId = null, string accessToken = null)
    {
        var request = new HttpRequestMessage(method, $"https://api.bigcommerce.com/{storeHash}/{endpoint}");
        request.Headers.Add("Accept", "application/json");
        if (!string.IsNullOrEmpty(clientId))
            request.Headers.Add("X-Auth-Client", clientId);

        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Add("X-Auth-Token", accessToken);

        if (method == HttpMethod.Post || method == HttpMethod.Put)
            request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"An error occurred: Request status code: {e.StatusCode}...  Exception message: {e.Message}");
            throw;
        }
    }

    private async Task<T> DeserializeOrLog<T>(HttpResponseMessage result, string what) where T : class
    {
        var body = await result.Content.ReadAsStringAsync();
        try
        {
            return JsonConvert.DeserializeObject<T>(body);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Could not parse BigCommerce response for {what}: {body}... {ex.Message}");
            return null;
        }
    }
}

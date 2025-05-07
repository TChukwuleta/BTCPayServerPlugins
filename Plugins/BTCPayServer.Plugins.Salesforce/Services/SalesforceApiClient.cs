using Newtonsoft.Json;
using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using BTCPayServer.Plugins.Salesforce.Data;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class SalesforceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SalesforceApiClientCredentials _credentials;
    public SalesforceApiClient(IHttpClientFactory httpClientFactory, SalesforceApiClientCredentials credentials)
    {
        _credentials = credentials;
        _httpClient = httpClientFactory != null ? httpClientFactory.CreateClient(nameof(SalesforceApiClient)) : new HttpClient();
    }


    public async Task<AuthResponse> Authenticate(SalesforceSetting salesforceSetting, string loginUrl = "https://login.salesforce.com")
    {
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"grant_type", "password"},
                {"client_id", salesforceSetting.ConsumerKey},
                {"client_secret", salesforceSetting.ConsumerSecret},
                {"username", salesforceSetting.Username},
                {"password", salesforceSetting.Password}
            });
        var response = await _httpClient.PostAsync($"{loginUrl}/services/oauth2/token", formContent);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Authentication failed: {errorContent}");
        }
        var jsonResponse = await response.Content.ReadAsStringAsync();
        var authResponse = JsonConvert.DeserializeObject<AuthResponse>(jsonResponse);
        authResponse.instance_url?.TrimEnd('/');
        return authResponse;
    }

    public async Task UpdateSalesforceOrder(SalesforceSetting salesforceSetting, string orderId, string status)
    {
        var authResponse = await Authenticate(salesforceSetting);
        var url = $"{authResponse.instance_url}/services/data/v58.0/sobjects/Order/{orderId}";
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.access_token);
        var payload = new
        {
            Status = status,
            Payment_Status__c = status
        };
        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Salesforce update failed: {response.StatusCode} - {body}");
        }
        Console.WriteLine($"Updated order {orderId} status to {status}");
    }

    public async Task CreatePaymentStatusField(AuthResponse authResponse)
    {
        var picklistField = new
        {
            FullName = "Order.Payment_Status__c",
            Metadata = new
            {
                label = "Payment Status",
                description = "Status of the Bitcoin payment",
                required = false,
                type = "Picklist",
                valueSet = new
                {
                    restricted = true,
                    valueSetDefinition = new
                    {
                        sorted = false,
                        value = new[]
                        {
                            new { fullName = "Pending", @default = false, label = "Pending" },
                            new { fullName = "Payment Received", @default = false, label = "Payment Received" },
                            new { fullName = "Payment Confirmed", @default = false, label = "Payment Confirmed" },
                            new { fullName = "Payment Expired", @default = false, label = "Payment Expired" }
                        }
                    }
                }
            }
        };
        await CreateCustomField(picklistField, authResponse);
    }


    public async Task CreateBTCInvoiceIdField(AuthResponse authResponse)
    {
        var textField = new
        {
            FullName = "Order.BTC_Invoice_ID__c",
            Metadata = new
            {
                label = "BTC Invoice ID",
                description = "BTCPay Server Invoice ID",
                required = false,
                type = "Text",
                length = 50,
                unique = false
            }
        };
        await CreateCustomField(textField, authResponse);
    }

    public async Task CreateBTCPaymentUrlField(AuthResponse authResponse)
    {
        // Create a URL field for BTC Payment URL
        var urlField = new
        {
            FullName = "Order.BTC_Payment_URL__c",
            Metadata = new
            {
                label = "BTC Payment URL",
                description = "BTCPay Server Payment URL",
                required = false,
                type = "Url"
            }
        };
        await CreateCustomField(urlField, authResponse);
    }

    private async Task CreateCustomField(object fieldDefinition, AuthResponse authResponse)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.access_token);
            string url = $"{authResponse.instance_url}/services/data/v58.0/tooling/sobjects/CustomField/";
            var content = new StringContent(JsonConvert.SerializeObject(fieldDefinition), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Field created successfully: {responseContent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            throw new Exception(ex.Message, ex);
        }
    }

    public class AuthResponse
    {
        public string access_token { get; set; }
        public string instance_url { get; set; }
        public string id { get; set; }
        public string token_type { get; set; }
        public string issued_at { get; set; }
        public string signature { get; set; }
    }
    public class SalesforceApiClientCredentials
    {
        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}

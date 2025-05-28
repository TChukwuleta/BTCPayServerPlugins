using Newtonsoft.Json;
using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using BTCPayServer.Plugins.Salesforce.Data;
using System.Linq;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class SalesforceApiClient
{
    private readonly HttpClient _httpClient;
    public SalesforceApiClient(IHttpClientFactory httpClientFactory)
    {
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
                {"password", $"{salesforceSetting.Password}{salesforceSetting.SecurityToken}"}
            });
        var response = await _httpClient.PostAsync($"{loginUrl}/services/oauth2/token", formContent);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Authentication failed: {errorContent}");
        }
        var jsonResponse = await response.Content.ReadAsStringAsync();
        Console.WriteLine(jsonResponse);
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


    public async Task UpdateTransactionStatus(SalesforceSetting salesforceSetting, string invoiceId, string status)
    {
        try
        {
            var authResponse = await Authenticate(salesforceSetting);
            var soql = $"SELECT Id FROM BTCPay_Transaction__c WHERE Invoice_ID__c = '{invoiceId}' LIMIT 1";
            var req = new HttpRequestMessage(HttpMethod.Get, $"{authResponse.instance_url}/{$"/services/data/v58.0/query/?q={Uri.EscapeDataString(soql)}"}");
            var response = await SendRequest(req, authResponse.access_token);
            if (response.isSuccess)
            {
                var responseModel = JsonConvert.DeserializeObject<SalesforceQueryResponse<SalesforceRecord>>(response.responseContent, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include
                });
                if (responseModel.records?.Any() == true)
                {
                    var transactionId = responseModel.records.First().Id;
                    var updateData = new { Status__c = MapBTCPayStatusToSalesforce(status) };
                    await UpdateRecordAsync("BTCPay_Transaction__c", transactionId, updateData, authResponse);
                }
            }
        }
        catch (Exception){}
    }

    public async Task<bool> UpdateRecordAsync(string objectName, string recordId, object updateData, AuthResponse authResponse)
    {
        try
        {
            var json = JsonConvert.SerializeObject(updateData);
            var req = new HttpRequestMessage(HttpMethod.Patch, $"{authResponse.instance_url}/services/data/v58.0/sobjects/{objectName}/{recordId}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await SendRequest(req, authResponse.access_token);
            return response.isSuccess;
        }
        catch (Exception) { return false; }
    }

    public async Task<string> CreateRecordAsync(SalesforceSetting salesforceSetting, string objectName, object recordData)
    {
        try
        {
            var authResponse = await Authenticate(salesforceSetting);
            var json = JsonConvert.SerializeObject(recordData);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{authResponse.instance_url}/services/data/v58.0/sobjects/{objectName}/");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await SendRequest(req, authResponse.access_token);
            if (response.isSuccess)
            {
                var responseModel = JsonConvert.DeserializeObject<SalesforceCreateResponse>(response.responseContent, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include
                });
                return responseModel.id;
            }
            else
            {
                return null;
            }
        }
        catch (Exception){ return null; }
    }

    private async Task<(string responseContent, bool isSuccess)> SendRequest(HttpRequestMessage req, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        Console.WriteLine(accessToken);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        var requestBody = await req.Content.ReadAsStringAsync();
        Console.WriteLine(JsonConvert.SerializeObject(requestBody));
        Console.WriteLine(responseContent);
        return (responseContent, response.IsSuccessStatusCode);
    }

    private string MapBTCPayStatusToSalesforce(string btcpayStatus)
    {
        return btcpayStatus.ToLower() switch
        {
            "new" => "New",
            "processing" => "Processing",
            "settled" => "Settled",
            "invalid" => "Invalid",
            "expired" => "Expired",
            _ => "New"
        };
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

    public class SalesforceCreateResponse
    {
        public string id { get; set; } = string.Empty;
        public bool success { get; set; }
        public List<SalesforceError> errors { get; set; } = new();
    }

    public class SalesforceQueryResponse<T>
    {
        public int totalSize { get; set; }
        public bool done { get; set; }
        public List<T> records { get; set; } = new();
        public string? nextRecordsUrl { get; set; }
    }

    public class SalesforceRecord
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, object> attributes { get; set; } = new();
    }

    public class SalesforceQueryResponse
    {
        [JsonProperty("records")]
        public SalesforceRecord[] Records { get; set; }
    }

    public class SalesforceError
    {
        public string message { get; set; } = string.Empty;
        public string errorCode { get; set; } = string.Empty;
        public List<string> fields { get; set; } = new();
    }
}

using Newtonsoft.Json;
using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using BTCPayServer.Plugins.Salesforce.Data;
using System.Linq;
using NBitpayClient;
using System.Security.AccessControl;

namespace BTCPayServer.Plugins.Salesforce.Services;

public class SalesforceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string customObjectName = "BTCPay_Server_Settings";
    private readonly string customObjectLabel = "BTCPay Server Settings";
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
        var authResponse = JsonConvert.DeserializeObject<AuthResponse>(jsonResponse);
        authResponse.instance_url?.TrimEnd('/');
        return authResponse;
    }

    public async Task<bool> CreatePaymentGatewayProvider(SalesforceSetting salesforceSetting)
    {
        var authResponse = await Authenticate(salesforceSetting);
        Console.WriteLine(authResponse.instance_url);
        Console.WriteLine(authResponse.access_token);
        var payload = new
        {
            Fullname = "BTCPayServerGatewayProvider",
            Metadata = new
            {
                fullName = "BTCPayServer",
                apexAdapter = "BTCPayServerGatewayProvider",
                idempotencySupported = "Yes",
                comments = "BTCPay Server-Salesforce gateway provider",
                masterLabel = "BTCPay Payment Gateway Provider"
            }
        };
        var json = JsonConvert.SerializeObject(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{authResponse.instance_url}/services/data/v62.0/tooling/sobjects/PaymentGatewayProvider");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, authResponse.access_token);
        return response.isSuccess;
    }

    public async Task SetupCustomObject(SalesforceSetting salesforceSetting, string serverUrl, string storeId)
    {
        var authResponse = await Authenticate(salesforceSetting);
        await CreateBTCPaySettingsField(authResponse, "Server_URL", "Server URL");
        await CreateBTCPaySettingsField(authResponse, "Store_ID", "Store ID");
        await InsertBTCPaySettingsData(authResponse, serverUrl, storeId);
    }

    private async Task<bool> CreateBTCPaySettingsObject(AuthResponse authResponse)
    {
        string url = $"{authResponse.instance_url}/services/data/v60.0/tooling/sobjects/CustomObject/";
        var payload = new
        {
            DeveloperName = customObjectName,
            PluralLabel = $"{customObjectLabel}s",
            NameField = new
            {
                Type = "Text",
                Label = "Name"
            },
            DeploymentStatus = "Deployed",
            Description = "BTCPay Server configuration for each org",
            SharingModel = "ReadWrite",
            CustomSettingsType = "Hierarchy",
            EnableFeeds = false
        };
        return await CreateCustomData(payload, authResponse, url);
    }

    private async Task<bool> CreateBTCPaySettingsField(AuthResponse authResponse, string fieldName, string label)
    {
        string url = $"{authResponse.instance_url}/services/data/v60.0/tooling/sobjects/CustomField/";
        var payload = new
        {
            FullName = $"{customObjectName}__c.{fieldName}__c",
            Metadata = new
            {
                type = "Text",
                label,
                length = 255
            }
        };
        return await CreateCustomData(payload, authResponse, url);
    }

    private async Task<bool> InsertBTCPaySettingsData(AuthResponse authResponse, string serverUrl, string storeId)
    {
        var payload = new
        {
            Name = "Default",
            Server_URL__c = serverUrl,
            Store_ID__c = storeId
        };
        var json = JsonConvert.SerializeObject(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{authResponse.instance_url}/services/data/v60.0/sobjects/{customObjectName}__c");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, authResponse.access_token);
        return response.isSuccess;
    }

    private async Task<bool> CreateCustomData(object fieldDefinition, AuthResponse authResponse, string url)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.access_token);
        var content = new StringContent(JsonConvert.SerializeObject(fieldDefinition), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Field created successfully: {responseContent}");
        return response.IsSuccessStatusCode;
    }

    public async Task RegisterBTCPayStoreInSalesforce(SalesforceSetting salesforceSetting, string serverUrl, string storeId)
    {
        var authResponse = await Authenticate(salesforceSetting);
        var payload = new { serverUrl, storeId };
        var json = JsonConvert.SerializeObject(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{authResponse.instance_url}/services/apexrest/btcpay/register");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, authResponse.access_token);
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

    public async Task WebhookNotification(SalesforceSetting salesforceSetting, string invoiceId, string status, string storeId, string amount)
    {
        var soql = $"SELECT Id FROM PaymentGatewayProvider WHERE DeveloperName = 'BTCPayServer'";
        var paymentGatewayProvider = await FetchIdUsingSoqlQuery(salesforceSetting, soql);
        var payload = new
        {
            invoiceId,
            status,
            storeId,
            amountPaid = amount,
        };
        var json = JsonConvert.SerializeObject(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{paymentGatewayProvider.response.instance_url}/solutions/services/data/v58.0/commerce/payments/notify?provider={paymentGatewayProvider.id}");
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, paymentGatewayProvider.response.access_token);
    }


    public async Task<(AuthResponse response, string id)> FetchIdUsingSoqlQuery(SalesforceSetting salesforceSetting, string soql)
    {
        string id = string.Empty;
        var authResponse = await Authenticate(salesforceSetting);
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{authResponse.instance_url}/{$"/services/data/v62.0/query/?q={Uri.EscapeDataString(soql)}"}");
            var response = await SendRequest(req, authResponse.access_token);
            Console.WriteLine(response.responseContent);
            if (!response.isSuccess)
            {
                var responseModel = JsonConvert.DeserializeObject<SalesforceQueryResponse<SalesforceRecord>>(response.responseContent, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include
                });
                id = responseModel.records?.FirstOrDefault()?.Id;
            }
        }
        catch (Exception) { }
        return (authResponse, id);
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

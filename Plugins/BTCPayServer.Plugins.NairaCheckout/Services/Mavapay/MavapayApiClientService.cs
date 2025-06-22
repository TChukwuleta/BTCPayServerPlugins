using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using Newtonsoft.Json;
using BTCPayServer.Plugins.NairaCheckout.Data;
using BTCPayServer.Services.Invoices;
using System.Linq;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class MavapayApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    public readonly string ApiUrl = "https://staging.api.mavapay.co/api/v1";
    private readonly List<string> validStatuses = new List<string> { "success", "ok" };

    public MavapayApiClientService(IHttpClientFactory httpClientFactory, NairaCheckoutDbContextFactory dbContextFactory, InvoiceRepository invoiceRepository)
    {
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClient = httpClientFactory?.CreateClient(nameof(MavapayApiClientService)) ?? new HttpClient();
    }

    public async Task<NairaCheckoutResponseViewModel> NairaCheckout(string apikey, decimal amount, string lnInvoice, string invoiceId, string storeId)
    { 
        try
        {
            var createQuote = await CreateQuote(new CreateQuoteRequestVm
            {
                amount = amount,
                customerInternalFee = 0,
                sourceCurrency = "NGNKOBO",
                targetCurrency = "BTCSAT",
                paymentMethod = "BankTransfer",
                beneficiary = new MavapayBeneficiaryVm { lnInvoice = lnInvoice }
            }, apikey);
            if (createQuote == null || string.IsNullOrEmpty(createQuote.id))
            {
                return new NairaCheckoutResponseViewModel { ErrorMessage = "An error occured while creating record via Mavapay. Please contact the merchant" };
            }
            var amountInNaira = createQuote.amountInSourceCurrency / 100m; // display to user amount in source currency.. amount is usually in Kobo
            await CreateOrderRecord(createQuote, invoiceId, amount, storeId);
            return new NairaCheckoutResponseViewModel { BankName = createQuote.bankName, AccountNumber = createQuote.ngnBankAccountNumber, AccountName = createQuote.ngnAccountName, Amount = amountInNaira };
        }
        catch (Exception ex)
        {
            return new NairaCheckoutResponseViewModel { ErrorMessage = $"An error occured: {ex.Message}. Please contact the merchant" };
        }
    }

    public async Task<(decimal amount, string lnInvoice)> GetLightningPaymentLink(string invoiceId)
    {
        var entity = await _invoiceRepository.GetInvoice(invoiceId, true);
        var prompt = entity.GetPaymentPrompts().FirstOrDefault(p => p.PaymentMethodId.ToString() == "BTC-LN");
        if (prompt is null || !prompt.Activated)
            return (0, string.Empty);

        var accounting = prompt.Currency is not null ? prompt.Calculate() : null;
        return (accounting?.TotalDue ?? 0m, prompt.Destination);
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
        return validStatuses.Contains(responseModel.status?.ToLower().Trim());
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
        return validStatuses.Contains(responseModel.status?.ToLower().Trim());
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

    private async Task CreateOrderRecord(CreateQuoteResponseVm quoteResponse, string invoiceId, decimal amount, string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        ctx.NairaCheckoutOrders.Add(new NairaCheckoutOrder
        {
            StoreId = storeId,
            Amount = amount.ToString(),
            InvoiceId = invoiceId,
            ExternalHash = quoteResponse.hash,
            ExternalReference = quoteResponse.id,
            BTCPayMarkedPaid = false,
            InvoiceStatus = "New",
            ThirdPartyStatus = "New",
            ThirdPartyMarkedPaid = false,
            CreatedDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl) => new HttpRequestMessage(method, $"{ApiUrl}/{relativeUrl}");

    private async Task<string> SendRequest(HttpRequestMessage req, string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine(responseContent);
        return responseContent;
    }
}

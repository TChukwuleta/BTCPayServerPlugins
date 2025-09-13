using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.NairaCheckout.Data;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Services.Invoices;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class MavapayApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    public readonly string ApiUrl = "https://staging.api.mavapay.co/api/v1"; //"https://staging.api.mavapay.co/api/v1";
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
                amount = amount * 100000000m,
                autopayout = true,
                customerInternalFee = 0,
                sourceCurrency = "NGNKOBO",
                targetCurrency = "BTCSAT",
                paymentMethod = "BankTransfer",
                beneficiary = new MavapayBeneficiaryVm { lnInvoice = lnInvoice }
            }, invoiceId, apikey);
            if (createQuote == null || string.IsNullOrEmpty(createQuote.id))
            {
                return new NairaCheckoutResponseViewModel { ErrorMessage = "An error occured while creating record via Mavapay. Please contact the merchant" };
            }
            var amountInNaira = createQuote.amountInSourceCurrency / 100m; // display to user amount in source currency.. amount is usually in Kobo, but user needs to see Naira
            await CreateOrderRecord(createQuote, invoiceId, amount, storeId);
            return new NairaCheckoutResponseViewModel { 
                BankName = createQuote.bankName, 
                AccountNumber = createQuote.ngnBankAccountNumber, 
                AccountName = createQuote.ngnAccountName, 
                Amount = amountInNaira,
                AccountNumberExpiration = createQuote.expiry,
            };
        }
        catch (Exception ex)
        {
            return new NairaCheckoutResponseViewModel { ErrorMessage = $"An error occured: {ex.Message}. Please contact the merchant" };
        }
    }

    public async Task<CreatePayoutResponseModel> MavapayNairaPayout(PayoutNGNViewModel model, string apikey)
    {
        try
        {
            var createPayout = await CreatePayout(apikey, model.Amount, "NGNKOBO", new  
            { 
                bankAccountName = model.AccountName, 
                bankAccountNumber = model.AccountNumber, 
                bankCode = model.BankCode, 
                bankName = model.BankName 
            });
            if (createPayout == null || string.IsNullOrEmpty(createPayout.id))
            {
                return new CreatePayoutResponseModel { ErrorMessage = "An error occured while creating payout record via Mavapay. Please contact the merchant" };
            }
            return createPayout;
        }
        catch (Exception ex)
        {
            return new CreatePayoutResponseModel { ErrorMessage = $"An error occured: {ex.Message}. Please contact the merchant" };
        }
    }

    public async Task<CreatePayoutResponseModel> MavapayRandsPayout(PayoutZARViewModel model, string apikey)
    {
        try
        {
            var createPayout = await CreatePayout(apikey, model.Amount, "ZARCENT", new
            {
                name = model.AccountName,
                bankName = model.Bank,
                bankAccountNumber = model.AccountNumber,
            });
            if (createPayout == null || string.IsNullOrEmpty(createPayout.id))
            {
                return new CreatePayoutResponseModel { ErrorMessage = "An error occured while creating payout record via Mavapay. Please contact the merchant" };
            }
            return createPayout;
        }
        catch (Exception ex)
        {
            return new CreatePayoutResponseModel { ErrorMessage = $"An error occured: {ex.Message}. Please contact the merchant" };
        }
    }

    public async Task<CreatePayoutResponseModel> MavapayKenyanShillingPayout(PayoutKESViewModel model, string apikey)
    {
        CreatePayoutResponseModel createPayout = new();
        try
        {
            switch (model.Method)
            {
                case "PhoneNumber":
                    createPayout = await CreatePayout(apikey, model.Amount, "KESCENT", new
                    {
                        identifierType = "paytophone",
                        identifiers = new { phoneNumber = model.Identifier }
                    });
                    break;
                case "TillNumber":
                    createPayout = await CreatePayout(apikey, model.Amount, "KESCENT", new
                    {
                        identifierType = "paytotill",
                        identifiers = new { tillNumber = model.Identifier }
                    });
                    break;
                case "BillNumber":
                    if (string.IsNullOrEmpty(model.AccountNumber))
                    {
                        return new CreatePayoutResponseModel { ErrorMessage = "Please provide an account number for the Bill number" };
                    }
                    createPayout = await CreatePayout(apikey, model.Amount, "KESCENT", new
                    {
                        identifierType = "paytobill",
                        identifiers = new { paybillNumber = model.Identifier, accountNumber = model.AccountNumber }
                    });
                    break;
                default: return new CreatePayoutResponseModel { ErrorMessage = "Invalid Kenyan shilling payment method" };
            }
            if (createPayout == null || string.IsNullOrEmpty(createPayout.id))
            {
                return new CreatePayoutResponseModel { ErrorMessage = "An error occured while creating payout record via Mavapay. Please contact the merchant" };
            }
            return createPayout;
        }
        catch (Exception ex)
        {
            return new CreatePayoutResponseModel { ErrorMessage = $"An error occured: {ex.Message}. Please contact the merchant" };
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

    public async Task<CreateQuoteResponseVm> CreateQuote(CreateQuoteRequestVm requestModel, string invoiceId, string apiKey)
    {
        var result = new InvoiceLogs();
        result.Write($"Initiating call to mavapay to create quote", InvoiceEventData.EventSeverity.Info);
        var postJson = JsonConvert.SerializeObject(requestModel);
        var req = CreateRequest(HttpMethod.Post, "quote");
        req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, apiKey);
        result.Write($"Mavapay quote response: {response}", InvoiceEventData.EventSeverity.Info);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<CreateQuoteResponseVm>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        await _invoiceRepository.AddInvoiceLogs(invoiceId, result);
        return responseModel.data;
    }

    public async Task<CreatePayoutResponseModel> CreatePayout(string apiKey, decimal amount, string currency, object beneficiary)
    {
        var postJson = JsonConvert.SerializeObject(new CreatePayoutRequestVm
        {
            amount = amount * 100m,
            customerInternalFee = 0,
            sourceCurrency = "BTCSAT",
            targetCurrency = currency,
            paymentMethod = "LIGHTNING",
            autopayout = true,
            paymentCurrency = currency,
            beneficiary = beneficiary
        });
        var req = CreateRequest(HttpMethod.Post, "quote");
        req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<CreatePayoutResponseModel>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        return responseModel.data;
    }

    public async Task<List<GetNGNBanks>> GetNGNBanks(string apiKey)
    {
        var req = CreateRequest(HttpMethod.Get, "bank/bankcode?country=NG");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<List<GetNGNBanks>>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        if (responseModel == null || !validStatuses.Contains(responseModel.status?.ToLower().Trim()))
            return null;
        return responseModel.data;
    }

    public async Task<List<string>> GetZARBanks(string apiKey)
    {
        var req = CreateRequest(HttpMethod.Get, "bank/bankcode?country=ZA");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<List<string>>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        if (responseModel == null || !validStatuses.Contains(responseModel.status?.ToLower().Trim()))
            return null;
        return responseModel.data;
    }

    public async Task<NGNNameEquiry> NGNNameEnquiry(string bankCode, string accountNumber, string apiKey)
    {
        var req = CreateRequest(HttpMethod.Get, $"bank/name-enquiry?accountNumber={accountNumber}&bankCode={bankCode}");
        var response = await SendRequest(req, apiKey);
        var responseModel = JsonConvert.DeserializeObject<EntityVm<NGNNameEquiry>>(response, new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        });
        if (responseModel == null || !validStatuses.Contains(responseModel.status?.ToLower().Trim()))
            return null;
        return responseModel.data;
    }

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

    public async Task<List<TransactionResponseVm>> GetMavapayTransactionRecord(string apiKey, string id = null, string orderId = null, string hash = null)
    {
        try
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
            var responseModel = JsonConvert.DeserializeObject<EntityVm<List<TransactionResponseVm>>>(response, new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            });
            return responseModel.data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task MarkTransactionStatusAsSuccess(NairaCheckoutDbContext ctx, string externalReference, string storeId)
    {
        var payout = ctx.PayoutTransactions.FirstOrDefault(c => c.StoreId == storeId && c.ExternalReference.EndsWith(":" + externalReference) && c.Provider == Wallet.Mavapay.ToString());
        if (payout == null) return;

        payout.IsSuccess = true;
        ctx.PayoutTransactions.Update(payout);
        await ctx.SaveChangesAsync();
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
        const string headerKey = "X-API-KEY";
        if (req.Headers.Contains(headerKey))
            req.Headers.Remove(headerKey);

        req.Headers.Add(headerKey, apiKey);
        var response = await _httpClient.SendAsync(req);
        var responseContent = await response.Content.ReadAsStringAsync();
        return responseContent;
    }
}

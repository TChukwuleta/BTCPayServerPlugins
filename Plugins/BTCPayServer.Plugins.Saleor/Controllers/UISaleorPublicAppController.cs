using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Plugins.Saleor.Services;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Saleor;

[AllowAnonymous]
[Route("~/plugins/{storeId}/saleor/")]
public class UISaleorPublicAppController : Controller
{
    private readonly SaleorAplService _apl;
    private readonly SaleorWebhookVerifier _verifier;
    private readonly StoreRepository _storeRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UIInvoiceController _invoiceController;
    private readonly ILogger<UISaleorPublicAppController> _logger;
    public UISaleorPublicAppController(SaleorAplService apl, SaleorWebhookVerifier verifier,  StoreRepository storeRepository, 
        InvoiceRepository invoiceRepository, UIInvoiceController invoiceController, ILogger<UISaleorPublicAppController> logger,
        IHttpClientFactory httpClientFactory)
    {

        _apl = apl;
        _verifier = verifier;
        _logger = logger;
        _storeRepository = storeRepository;
        _httpClientFactory = httpClientFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }

    #region Manifest 

    [HttpGet("api/manifest")]
    public IActionResult GetManifest()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}/plugins/btcpay-saleor";

        var manifest = new AppManifest
        {
            Id = "saleor.app.btcpay",
            Version = "1.0.0",
            Name = "BTCPay Server",
            Author = "BTCPay Server",
            About = "Accept Bitcoin payments via your self-hosted BTCPay Server",
            Permissions = ["MANAGE_ORDERS", "HANDLE_PAYMENTS"],
            AppUrl = baseUrl,
            TokenTargetUrl = $"{baseUrl}/register",
            Brand = new BrandManifest
            {
                Logo = new LogoManifest
                {
                    Default = $"{baseUrl}/btcpay_logo.png"
                }
            },
            Webhooks =
            [
                new WebhookManifest
                {
                    Name = "Payment Gateway Initialize Session",
                    SyncEvents = ["PAYMENT_GATEWAY_INITIALIZE_SESSION"],
                    TargetUrl = $"{baseUrl}/webhooks/payment-gateway-initialize-session",
                    Query = """
                        subscription {
                            event {
                                ... on PaymentGatewayInitializeSession {
                                    sourceObject {
                                        ... on Checkout { id channel { slug } }
                                        ... on Order { id channel { slug } }
                                    }
                                    data
                                    amount
                                }
                            }
                        }
                        """
                },
                new WebhookManifest
                {
                    Name = "Transaction Initialize Session",
                    SyncEvents = ["TRANSACTION_INITIALIZE_SESSION"],
                    TargetUrl = $"{baseUrl}/webhooks/transaction-initialize-session",
                    Query = """
                        subscription {
                            event {
                                ... on TransactionInitializeSession {
                                    transaction { id pspReference }
                                    action { amount currency actionType }
                                    sourceObject {
                                        ... on Checkout { channel { slug } }
                                        ... on Order { channel { slug } }
                                    }
                                    data
                                }
                            }
                        }
                        """
                },
                new WebhookManifest
                {
                    Name = "Transaction Process Session",
                    SyncEvents = ["TRANSACTION_PROCESS_SESSION"],
                    TargetUrl = $"{baseUrl}/webhooks/transaction-process-session",
                    Query = """
                        subscription {
                            event {
                                ... on TransactionProcessSession {
                                    transaction { id pspReference }
                                    action { amount currency actionType }
                                    data
                                }
                            }
                        }
                        """
                }
            ],
            Extensions = []
        };

        return Ok(manifest);
    }

    #endregion


    #region Transaction Webhook

    static AsyncDuplicateLock OrderLocks = new AsyncDuplicateLock();
    [HttpPost("webhook/transaction-initialize-session")]
    public async Task<IActionResult> TransactionInitializeSession(string storeId)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();
        var signature = Request.Headers["saleor-signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest("Missing saleor-api-url header");

        var authData = await _apl.GetAsync(saleorApiUrl);
        if (authData is null)
            return Unauthorized("Saleor instance not registered");

        var rawBody = await _verifier.ReadRawBodyAsync(Request);
        if (!_verifier.Verify(rawBody, signature ?? "", authData.Token))
            return Unauthorized("Invalid webhook signature");

        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        TransactionInitializeSessionPayload payload;
        decimal amount = 0;
        try
        {
            payload = JsonSerializer.Deserialize<TransactionInitializeSessionPayload>(rawBody) ?? throw new Exception("Empty payload");

            amount = payload.Action.Amount;
            var transactionId = payload.Transaction.Id;
            var searchTerm = $"{Extensions.SALEOR_ORDER_ID_PREFIX}{payload.Transaction.Id}";

            using var _ = await OrderLocks.LockAsync(transactionId, CancellationToken.None);

            var invoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
            {
                TextSearch = searchTerm,
                StoreId = new[] { storeId }
            });
            var existingInvoice = invoices.FirstOrDefault(e => e.GetSaleorOrderId() == payload.Transaction.Id);
            if (existingInvoice != null)
            {
                return Ok(new TransactionWebhookResponse
                {
                    Result = "CHARGE_ACTION_REQUIRED",
                    Amount = amount,
                    PspReference = existingInvoice.Id,
                    ExternalUrl = CheckoutUrl(existingInvoice.Id),
                    Data = new { url = CheckoutUrl(existingInvoice.Id), invoiceId = existingInvoice.Id },
                    Message = "Redirect customer to BTCPay to complete payment"
                });
            }

            var createInvoiceRequest = new CreateInvoiceRequest()
            {
                Amount = amount,
                Currency = payload.Action.Currency,
                Metadata = new JObject
                {
                    ["orderId"] = payload.Transaction.Id
                },
                AdditionalSearchTerms = [searchTerm]
            };

            if (payload.Data.HasValue && payload.Data.Value.TryGetProperty("redirectUrl", out var redirectProp))
            {
                var redirectUrl = redirectProp.GetString();
                if (!string.IsNullOrWhiteSpace(redirectUrl))
                {
                    createInvoiceRequest.Checkout = new() { RedirectURL = redirectUrl };
                }
            }

            var invoice = await _invoiceController.CreateInvoiceCoreRaw(createInvoiceRequest, store,
                Request.GetAbsoluteRoot(), [searchTerm], CancellationToken.None);

            return Ok(new TransactionWebhookResponse
            {
                Result = "CHARGE_ACTION_REQUIRED",
                Amount = amount,
                PspReference = invoice.Id,
                ExternalUrl = CheckoutUrl(invoice.Id),
                Data = new { url = CheckoutUrl(invoice.Id), invoiceId = invoice.Id },
                Message = "Redirect customer to BTCPay to complete payment"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create BTCPay invoice");
            return Ok(new TransactionWebhookResponse
            {
                Result = "CHARGE_FAILURE",
                Amount = amount,
                PspReference = Guid.NewGuid().ToString(),
                Message = $"BTCPay error: {ex.Message}"
            });
        }
    }

    [HttpPost("webhook/transaction-process-session")]
    public async Task<IActionResult> TransactionProcessSession(string storeId)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();
        var signature = Request.Headers["saleor-signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest("Missing saleor-api-url header");

        var authData = await _apl.GetAsync(saleorApiUrl);
        if (authData is null)
            return Unauthorized("Saleor instance not registered");

        var rawBody = await _verifier.ReadRawBodyAsync(Request);
        if (!_verifier.Verify(rawBody, signature ?? "", authData.Token))
            return Unauthorized("Invalid webhook signature");

        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        decimal amount = 0;
        try
        {
            var payload = JsonSerializer.Deserialize<TransactionProcessSessionPayload>(rawBody);
            if (payload is null) return BadRequest("Empty payload");

            amount = payload.Action.Amount;
            var pspReference = payload.Transaction?.PspReference;

            if (string.IsNullOrWhiteSpace(pspReference))
            {
                return Ok(new TransactionWebhookResponse
                {
                    Result = "CHARGE_FAILURE",
                    Amount = amount,
                    PspReference = Guid.NewGuid().ToString(),
                    Message = "Payment not initialized"
                });
            }

            var invoice = await _invoiceRepository.GetInvoice(pspReference);
            if (invoice is null)
            {
                return Ok(new TransactionWebhookResponse
                {
                    Result = "CHARGE_FAILURE",
                    Amount = amount,
                    PspReference = pspReference,
                    Message = "Invalid PSP reference"
                });
            }

            string result = invoice switch
            {
                { Status: InvoiceStatus.New } => "CHARGE_ACTION_REQUIRED",
                { Status: InvoiceStatus.Processing } => "CHARGE_SUCCESS",
                { Status: InvoiceStatus.Settled } => "CHARGE_SUCCESS",
                { Status: InvoiceStatus.Expired } => "CHARGE_FAILURE",
                { Status: InvoiceStatus.Invalid } => "CHARGE_FAILURE",
                _ => "CHARGE_ACTION_REQUIRED"
            };

            return Ok(new TransactionWebhookResponse
            {
                Result = result,
                Amount = amount,
                PspReference = invoice.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse TransactionProcessSession payload");
            return Ok(new TransactionWebhookResponse
            {
                Result = "CHARGE_FAILURE",
                Amount = amount,
                PspReference = Guid.NewGuid().ToString(),
                Message = ex.Message
            });
        }
    }

    #endregion

    #region Register


    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] SaleorRegisterRequest body)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest(new { error = "Missing saleor-api-url header" });

        if (string.IsNullOrEmpty(body.AuthToken))
            return BadRequest(new { error = "Missing auth_token" });

        // Fetch the app ID from Saleor using the token to verify it works
        // and to store the app ID for later metadata operations
        try
        {
            var appId = await FetchAppIdAsync(saleorApiUrl, body.AuthToken);
            await _apl.SetAsync(saleorApiUrl, body.AuthToken, appId ?? "");
            _logger.LogInformation("Registered Saleor instance: {Url}", saleorApiUrl);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify token for {Url}", saleorApiUrl);
            return StatusCode(500, new { error = "Failed to verify token with Saleor" });
        }
    }

    #endregion

    private string CheckoutUrl(string invoiceId) => Url.Action(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId }, Request.Scheme);

    private async Task<string?> FetchAppIdAsync(string saleorApiUrl, string token)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var query = JsonSerializer.Serialize(new { query = "query { app { id } }" });
        var content = new StringContent(query, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(saleorApiUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("app").GetProperty("id").GetString();
    }
}

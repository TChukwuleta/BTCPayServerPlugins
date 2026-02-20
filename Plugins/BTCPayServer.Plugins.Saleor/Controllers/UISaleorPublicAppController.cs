using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Filters;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Models;
using BTCPayServer.Plugins.Saleor.Services;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Saleor;

[AllowAnonymous]
[Route("~/plugins/{storeId}/saleor/")]
public class UISaleorPublicAppController : Controller
{
    private readonly SaleorAplService _apl;
    private readonly UriResolver _uriResolver;
    private readonly SaleorWebhookVerifier _verifier;
    private readonly StoreRepository _storeRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UIInvoiceController _invoiceController;
    public UISaleorPublicAppController(SaleorAplService apl, SaleorWebhookVerifier verifier,  StoreRepository storeRepository, 
        InvoiceRepository invoiceRepository, UIInvoiceController invoiceController, IHttpClientFactory httpClientFactory, UriResolver uriResolver)
    {

        _apl = apl;
        _verifier = verifier;
        _uriResolver = uriResolver;
        _storeRepository = storeRepository;
        _httpClientFactory = httpClientFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }

    static string FlattenQuery(string query) => Regex.Replace(query.Trim(), @"\s+", " ");

    #region Manifest 

    [HttpGet("api/manifest")]
    public IActionResult GetManifest(string storeId)
    {
        string Endpoint(string action) => Url.Action(action, "UISaleorPublicApp", new { storeId }, Request.Scheme);
        var manifest = new AppManifest
        {
            Id = "saleor.app.btcpay",
            Version = "1.0.0",
            Name = "BTCPay Server",
            Author = "BTCPay Server",
            About = "Accept Bitcoin payments via your self-hosted BTCPay Server",
            Permissions = ["HANDLE_PAYMENTS"],
            /*AppUrl = Endpoint(nameof(AppPage)),
            TokenTargetUrl = Endpoint(nameof(Register)),*/
            AppUrl= "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/app",
            TokenTargetUrl = "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/register",
            Brand = new BrandManifest
            {
                Logo = new LogoManifest { 
                    Default = "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/btcpay_logo.png" //Endpoint(nameof(Logo)) 
                }
            },
            Webhooks =
            [
                new WebhookManifest
                {
                    Name = "Payment Gateway Initialize Session",
                    SyncEvents = ["PAYMENT_GATEWAY_INITIALIZE_SESSION"],
                    TargetUrl = "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/webhooks/payment-gateway-initialize-session", //Endpoint(nameof(PaymentGatewayInitializeSession)),
                    Query = FlattenQuery("""
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
                    """)
                },
                new WebhookManifest
                {
                    Name = "Transaction Initialize Session",
                    SyncEvents = ["TRANSACTION_INITIALIZE_SESSION"],
                    TargetUrl = "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/webhooks/transaction-initialize-session", //Endpoint(nameof(TransactionInitializeSession)),
                    Query = FlattenQuery("""
                        subscription {
                            event {
                                ... on TransactionInitializeSession {
                                    transaction { id pspReference }
                                    action { amount currency actionType }
                                    sourceObject {
                                        ... on Checkout { 
                                            channel { slug }
                                            userEmail: email
                                        }
                                        ... on Order { 
                                            channel { slug }
                                            userEmail
                                        }
                                    }
                                    data
                                }
                            }
                        }
                    """)
                },
                new WebhookManifest
                {
                    Name = "Transaction Process Session",
                    SyncEvents = ["TRANSACTION_PROCESS_SESSION"],
                    TargetUrl = "https://1679-102-88-111-17.ngrok-free.app/plugins/862r7xnfBaYs42w2DUeStXg95ffsMw2NPkqp6jL28SZi/saleor/webhooks/transaction-process-session",  //Endpoint(nameof(TransactionProcessSession)),
                    Query = FlattenQuery("""
                        subscription {
                            event {
                                ... on TransactionProcessSession {
                                    transaction { id pspReference }
                                    action { amount currency actionType }
                                    data
                                }
                            }
                        }
                    """)
                }
            ],
            Extensions = []
        };
        return Ok(manifest);
    }

    #endregion


    #region App

    [HttpGet("btcpay_logo.png")]
    public IActionResult Logo()
    {
        var assembly = GetType().Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("btcpay_logo.png"));
        if (resourceName is null) return NotFound();

        var stream = assembly.GetManifestResourceStream(resourceName);
        return File(stream, "image/png");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(string storeId, [FromBody] JObject body)
    {
        var saleorApiUrl = Request.Headers["Saleor-Api-Url"].FirstOrDefault();
        var saleorDomain = Request.Headers["Saleor-Domain"].FirstOrDefault();
        var authToken = body["auth_token"]?.ToString();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest(new { error = "Missing saleor-api-url header" });

        if (string.IsNullOrEmpty(authToken))
            return BadRequest(new { error = "Missing auth_token" });

        try
        {
            var appId = await FetchAppIdAsync(saleorApiUrl, authToken);
            await _apl.Set(saleorApiUrl, authToken, storeId, saleorDomain, appId ?? "");
            return Ok(new { success = true });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = "Failed to verify token with Saleor" });
        }
    }

    [HttpGet("app")]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    public async Task<IActionResult> AppPage(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        var entry = await _apl.Get(storeId);
        var vm = new SaleorAppPageViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            ConnectedInstance = entry,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
        };
        return View(vm);
    }

    [HttpGet("api/status")]
    public async Task<IActionResult> GetStatus(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        var entry = await _apl.Get(storeId);
        return Ok(new
        {
            storeId,
            storeName = store.StoreName,
            connected = !string.IsNullOrEmpty(entry.Token),
            saleorApiUrl = entry?.SaleorApiUrl,
            registeredAt = entry?.RegisteredAt
        });
    }

    [HttpPost("api/disconnect")]
    public async Task<IActionResult> Disconnect(string storeId, [FromBody] DisconnectRequest body)
    {
        if (string.IsNullOrEmpty(body.SaleorApiUrl))
            return BadRequest(new { error = "Missing saleorApiUrl" });

        await _apl.Delete(storeId);
        return Ok(new { ok = true });
    }

    #endregion


    #region Transaction Webhook

    static AsyncDuplicateLock OrderLocks = new AsyncDuplicateLock();

    [HttpPost("webhooks/payment-gateway-initialize-session")]
    public async Task<IActionResult> PaymentGatewayInitializeSession(string storeId)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();
        var signature = Request.Headers["saleor-signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest("Missing saleor-api-url header");

        var authData = await _apl.Get(storeId);
        if (authData is null || !authData.SaleorApiUrl.Equals(saleorApiUrl, StringComparison.OrdinalIgnoreCase))
            return Unauthorized("Saleor instance not registered or URL mismatch");

        var rawBody = await _verifier.ReadRawBodyAsync(Request);
        if (!await _verifier.Verify(rawBody, signature ?? "", saleorApiUrl))
            return Unauthorized("Invalid webhook signature");

        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        return Ok(new
        {
            data = new {
                provider = "btcpay",
                name = "Bitcoin (BTCPay Server)",
                errors = new ArrayList()
            }
        });
    }

    [HttpPost("webhooks/transaction-initialize-session")]
    public async Task<IActionResult> TransactionInitializeSession(string storeId)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();
        var signature = Request.Headers["saleor-signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest("Missing saleor-api-url header");

        var authData = await _apl.Get(storeId);
        if (authData is null || !authData.SaleorApiUrl.Equals(saleorApiUrl, StringComparison.OrdinalIgnoreCase))
            return Unauthorized("Saleor instance not registered or URL mismatch");

        var rawBody = await _verifier.ReadRawBodyAsync(Request);
        if (!await _verifier.Verify(rawBody, signature ?? "", saleorApiUrl))
            return Unauthorized("Invalid webhook signature");

        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        TransactionInitializeSessionPayload payload;
        decimal amount = 0;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<TransactionInitializeSessionPayload>(rawBody) ?? throw new Exception("Empty payload");

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
                    ["orderId"] = payload.Transaction.Id,
                    ["buyerEmail"] = payload.SourceObject?.UserEmail,
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
            return Ok(new TransactionWebhookResponse
            {
                Result = "CHARGE_FAILURE",
                Amount = amount,
                PspReference = Guid.NewGuid().ToString(),
                Message = $"BTCPay error: {ex.Message}"
            });
        }
    }

    [HttpPost("webhooks/transaction-process-session")]
    public async Task<IActionResult> TransactionProcessSession(string storeId)
    {
        var saleorApiUrl = Request.Headers["saleor-api-url"].FirstOrDefault();
        var signature = Request.Headers["saleor-signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(saleorApiUrl))
            return BadRequest("Missing saleor-api-url header");

        var authData = await _apl.Get(storeId);
        if (authData is null || !authData.SaleorApiUrl.Equals(saleorApiUrl, StringComparison.OrdinalIgnoreCase))
            return Unauthorized("Saleor instance not registered or URL mismatch");

        var rawBody = await _verifier.ReadRawBodyAsync(Request);
        if (!await _verifier.Verify(rawBody, signature ?? "", saleorApiUrl))
            return Unauthorized("Invalid webhook signature");

        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        decimal amount = 0;
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<TransactionProcessSessionPayload>(rawBody);
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


    private string CheckoutUrl(string invoiceId) => Url.Action(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId }, Request.Scheme);

    private async Task<string> FetchAppIdAsync(string saleorApiUrl, string token)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var query = System.Text.Json.JsonSerializer.Serialize(new { query = "query { app { id } }" });
        var content = new StringContent(query, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(saleorApiUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("app").GetProperty("id").GetString();
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using BTCPayServer.Abstractions.Extensions;
using System;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Controllers;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using Microsoft.Extensions.Logging;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Cors;
using System.Collections.Generic;
using NicolasDorier.RateLimits;
using System.Globalization;
using BTCPayServer.Plugins.ShopifyPlugin.Helper;
using BTCPayServer.Plugins.ShopifyPlugin.Services;
using BTCPayServer.Plugins.ShopifyPlugin;
using System.Net.Http;
using BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;
using BTCPayServer.Client.Models;
using BTCPayServer.Plugins.ShopifyPlugin.Data;
using Newtonsoft.Json.Linq;
using BTCPayServer.Payments;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIShopifyController : Controller
{
    private readonly ShopifyHostedService _shopifyService;
    private readonly ILogger<UIShopifyController> _logger;
    private readonly ILogger<ShopifyApiClient> _loggerrr;
    private readonly StoreRepository _storeRepo;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;
    private readonly ShopifyDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly IHttpClientFactory _clientFactory;
    private ShopifyHelper helper;
    public UIShopifyController
        (StoreRepository storeRepo,
        BTCPayNetworkProvider networkProvider,
        UIInvoiceController invoiceController,
        UserManager<ApplicationUser> userManager,
        ShopifyHostedService shopifyService,
        ShopifyDbContextFactory dbContextFactory,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory clientFactory,
        ILogger<UIShopifyController> logger,
        ILogger<ShopifyApiClient> loggerrr)
    {
        _storeRepo = storeRepo;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _shopifyService = shopifyService;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
        helper = new ShopifyHelper();
        _logger = logger;
        _loggerrr = loggerrr;
    }
    private const string SHOPIFY_ORDER_ID_PREFIX = "shopify-";
    public BTCPayServer.Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> Index(string storeId)
    {
        // http://localhost:14142/plugins/stores/6nxCxMtexeDAuGWVN7rZJFC7hwgykizfKNWryzU1XAt9/shopify
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        _logger.LogInformation($"Store has wallet: {storeHasWallet}");
        _logger.LogInformation($"Default crypto currency code: {_networkProvider.DefaultNetwork.CryptoCode}");
        if (!storeHasWallet)
        {
            return View(new ShopifySetting
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasStore = false 
            });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new ShopifySetting();
        return View(userStore);
    }

    [HttpPost("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> Index(string storeId,
            ShopifySetting vm, string command = "")
    {
        try
        {
            await using var ctx = _dbContextFactory.CreateContext();
            switch (command)
            {
                case "ShopifySaveCredentials":
                    {
                        var shopify = vm;
                        var validCreds = shopify != null && shopify?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Shopify credentials";
                            return View(vm);
                        }
                        var apiClient = new ShopifyApiClient(_clientFactory, shopify.CreateShopifyApiCredentials(), _loggerrr);
                        try
                        {
                            await apiClient.OrdersCount();
                        }
                        catch (ShopifyApiException err)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = err.Message;
                            return View(vm);
                        }

                        var scopesGranted = await apiClient.CheckScopes();
                        if (!scopesGranted.Contains("read_orders") || !scopesGranted.Contains("write_orders"))
                        {
                            TempData[WellKnownTempData.ErrorMessage] =
                                "Please grant the private app permissions for read_orders, write_orders";
                            return View(vm);
                        }
                        shopify.IntegratedAt = DateTimeOffset.UtcNow;
                        shopify.StoreId = CurrentStore.Id;
                        shopify.StoreName = CurrentStore.StoreName;
                        ctx.Update(shopify);
                        await ctx.SaveChangesAsync();

                        TempData[WellKnownTempData.SuccessMessage] = "Shopify plugin successfully updated";
                        break;
                    }
                case "ShopifyClearCredentials":
                    {
                        var shopifySetting = ctx.ShopifySettings.FirstOrDefault(c => !string.IsNullOrEmpty(vm.Id) && c.Id == vm.Id);
                        if (shopifySetting != null)
                        {
                            ctx.Remove(shopifySetting);
                            await ctx.SaveChangesAsync();
                        }
                        TempData[WellKnownTempData.SuccessMessage] = "Shopify plugin credentials cleared";
                        break;
                    }
            }
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"General error: {ex.Message}");
            throw;
        }
    }


    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/shopify.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (userStore == null)
        {
            return BadRequest("Invalid BTCPay store specified");
        }
    
        //var jsFile = await helper.GetCustomJavascript(userStore.StoreId, Request.GetAbsoluteRoot());
        var jsFile = await helper.GetCustomJavascript(userStore.StoreId, "https://0979-2c0f-2a80-78-f310-a079-b217-56c1-6c3f.ngrok-free.app");
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }


    [AllowAnonymous]
    [HttpGet("stores/{storeId}/plugins/shopify/invoice/{orderId}")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> ShopifyInvoiceEndpoint(
           string storeId, string orderId, decimal amount, bool checkOnly = false)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (userStore == null)
        {
            return BadRequest("Invalid BTCPay store specified");
        }

        var shopifySearchTerm = $"{SHOPIFY_ORDER_ID_PREFIX}{orderId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { userStore.StoreId }
        });
        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SHOPIFY_ORDER_ID_PREFIX)
                    .Any(s => s == orderId))
            .ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { InvoiceStatus.Processing.ToString(), InvoiceStatus.Settled.ToString() }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString()));

        var store = await _storeRepo.FindStore(userStore.StoreId);

        ShopifyApiClient client = null;
        ShopifyOrder order = null;
        if (userStore.IntegratedAt.HasValue)
        {
            client = new ShopifyApiClient(_clientFactory, userStore.CreateShopifyApiCredentials(), _loggerrr);
            order = await client.GetOrder(orderId);
            if (order?.Id is null)
            {
                return NotFound();
            }
        }
        _logger.LogInformation($"Gotten here.. order: {JsonConvert.SerializeObject(order)}");

        if (firstInvoiceSettled != null)
        {
            //if BTCPay was shut down before the tx managed to get registered on shopify, this will fix it on the next UI load in shopify
            if (client != null && order?.FinancialStatus == "pending" &&
                firstInvoiceSettled.Status.ToString() != InvoiceStatus.Processing.ToString())
            {
                await _shopifyService.Process(client, orderId, firstInvoiceSettled.Id,
                    firstInvoiceSettled.Currency,
                    firstInvoiceSettled.Price.ToString(CultureInfo.InvariantCulture), true);
                order = await client.GetOrder(orderId);
            }

            return Ok(new
            {
                invoiceId = firstInvoiceSettled.Id,
                status = firstInvoiceSettled.Status.ToString().ToLowerInvariant()
            });
        }

        if (checkOnly)
        {
            return Ok();
        }

        if (userStore.IntegratedAt.HasValue)
        {
            //we create the invoice at due amount provided from order page or full amount if due amount is bigger than order amount
            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = amount < order.TotalOutstanding ? amount : order.TotalOutstanding,
                    Currency = order.PresentmentCurrency,
                    Metadata = new JObject
                    {
                        ["orderId"] = order.OrderNumber,
                        ["shopifyOrderId"] = order.Id,
                        ["shopifyOrderNumber"] = order.OrderNumber
                    },
                    AdditionalSearchTerms = new[]
                    {
                            order.OrderNumber.ToString(CultureInfo.InvariantCulture),
                            order.Id.ToString(CultureInfo.InvariantCulture),
                            shopifySearchTerm
                    }
                }, store,
                Request.GetAbsoluteRoot(), new List<string>() { shopifySearchTerm });
            _logger.LogInformation($"Invoice... {JsonConvert.SerializeObject(invoice)}");

            return Ok(new { invoiceId = invoice.Id, status = invoice.Status.ToString().ToLowerInvariant() });
        }

        return NotFound();
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/shopify/initiate-payment-request")]
    public async Task<IActionResult> InitiatePaymentRequest(string storeId)
    {

        await using var ctx = _dbContextFactory.CreateContext();
        var exisitngStores = ctx.ShopifySettings.FirstOrDefault();

        var jsFile = await helper.GetCustomJavascript(storeId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }

    private static Dictionary<PaymentMethodId, JToken> GetPaymentMethodConfigs(BTCPayServer.Data.StoreData storeData, bool onlyEnabled = false)
    {
        if (string.IsNullOrEmpty(storeData.DerivationStrategies))
            return new Dictionary<PaymentMethodId, JToken>();
        var excludeFilter = onlyEnabled ? storeData.GetStoreBlob().GetExcludedPaymentMethods() : null;
        var paymentMethodConfigurations = new Dictionary<PaymentMethodId, JToken>();
        JObject strategies = JObject.Parse(storeData.DerivationStrategies);
        foreach (var strat in strategies.Properties())
        {
            if (!PaymentMethodId.TryParse(strat.Name, out var paymentMethodId))
                continue;
            if (excludeFilter?.Match(paymentMethodId) is true)
                continue;
            paymentMethodConfigurations.Add(paymentMethodId, strat.Value);
        }
        return paymentMethodConfigurations;
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

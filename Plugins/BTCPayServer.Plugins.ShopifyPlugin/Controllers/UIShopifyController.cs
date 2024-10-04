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
using BTCPayServer.Plugins.ShopifyPlugin.ViewModels;
using MailKit.Search;
using static BTCPayServer.HostedServices.PullPaymentHostedService.PayoutApproval;
using NBitpayClient;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIShopifyController : Controller
{
    private readonly ShopifyHostedService _shopifyService;
    private readonly ILogger<UIShopifyController> _logger;
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
        ILogger<UIShopifyController> logger)
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
    }
    private const string SHOPIFY_ORDER_ID_PREFIX = "shopify-";
    public Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> Index(string storeId)
    {
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
                        var apiClient = new ShopifyApiClient(_clientFactory, shopify.CreateShopifyApiCredentials());
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
                        shopify.ApplicationUserId = GetUserId();
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
    [HttpGet("~/stores/{invoiceId}/plugins/shopify/initiate-payment")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> InitiatePayment(string invoiceId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var transaction = ctx.Transactions.FirstOrDefault(c => c.InvoiceId == invoiceId && c.InvoiceStatus.ToLower().Equals("new"));
        if (transaction == null)
        {
            return BadRequest("Invalid BTCPay transaction specified");
        }
        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == transaction.ShopName);
        if (userStore == null)
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        return View(new ShopifyOrderViewModel
        {
            BTCPayServerUrl = Request.GetAbsoluteRoot(),
            InvoiceId = transaction.InvoiceId,
            OrderId = transaction.OrderId,
            ShopName = transaction.ShopName
        });
    }

    [AllowAnonymous]
    [HttpPost("~/stores/{storeId}/plugins/shopify/create-order")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateShopifyOrderRequest model, string storeId)
    {
        if (!storeId.Equals(model.storeId))
        {
            return BadRequest("Store Id mismatch");
        }
        var shopifySearchTerm = $"{SHOPIFY_ORDER_ID_PREFIX}{model.orderId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified");
        }
        ShopifyApiClient client = new ShopifyApiClient(_clientFactory, shopifySetting.CreateShopifyApiCredentials());
        ShopifyOrder order = await client.GetOrder(model.orderId);
        var store = await _storeRepo.FindStore(shopifySetting.StoreId);
        if (order == null)
        {
            return NotFound();
        }

        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { shopifySetting.StoreId }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SHOPIFY_ORDER_ID_PREFIX).Any(s => s == model.orderId)).ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        _logger.LogInformation($"First settled invoice: {JsonConvert.SerializeObject(firstInvoiceSettled)}");
        try
        {
            if (firstInvoiceSettled != null)
            {
                //if BTCPay was shut down before the tx managed to get registered on shopify, this will fix it on the next UI load in shopify
                if (order?.FinancialStatus == "pending" &&
                    firstInvoiceSettled.Status.ToString().ToLower() != InvoiceStatus.Processing.ToString().ToLower())
                {
                    await _shopifyService.Process(client, model.orderId, firstInvoiceSettled.Id,
                        firstInvoiceSettled.Currency,
                        firstInvoiceSettled.Price.ToString(CultureInfo.InvariantCulture), true, firstInvoiceSettled.Status.ToString().ToLower());
                    order = await client.GetOrder(model.orderId);
                }

                return Ok(new
                {
                    invoiceId = firstInvoiceSettled.Id,
                    status = firstInvoiceSettled.Status.ToString().ToLowerInvariant()
                });
            }


            _logger.LogInformation($"Model total is: {model.total}");
            _logger.LogInformation($"Total outstanding is: {order.TotalOutstanding}");
            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = Math.Max(model.total, order.TotalOutstanding),
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

            var entity = new Transaction
            {
                ShopName = storeId,
                StoreId = store.Id,
                OrderId = shopifySearchTerm,
                InvoiceId = invoice.Id,
                TransactionStatus = ShopifyPlugin.Data.TransactionStatus.Pending,
                InvoiceStatus = InvoiceStatus.New.ToString().ToLower(),
            };
            ctx.Add(entity);
            await ctx.SaveChangesAsync();

            return Ok(new { invoiceId = invoice.Id, status = invoice.Status.ToString().ToLowerInvariant() });
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred on creating order. Exception message: {ex.Message}");
            return BadRequest("An error occurred while trying to create order for Big Commerce");
        }
    }

    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/orders")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> RetrieveOrderDetails(string storeId, string checkoutToken)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified");
        }
        ShopifyApiClient client = new ShopifyApiClient(_clientFactory, shopifySetting.CreateShopifyApiCredentials());
        var orders = await client.RetrieveAllOrders();
        if (string.IsNullOrEmpty(checkoutToken))
        {
            orders = orders.Where(c => c.CheckoutToken == checkoutToken).ToList();
        }
        return Ok(ShopifyExtensions.GetShopifyOrderResponse(orders));
    }

    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/validate")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> ValidateShopifyAccount(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified");
        }
        return Ok(new { BTCPayStoreId = shopifySetting.StoreId, BTCPayStoreUrl = Request.GetAbsoluteRoot() });
    }

    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/btcpay-shopify.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.ShopName == storeId);
        if (userStore == null)
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        var jsFile = await helper.GetCustomJavascript(userStore.StoreId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }

    private static Dictionary<PaymentMethodId, JToken> GetPaymentMethodConfigs(Data.StoreData storeData, bool onlyEnabled = false)
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

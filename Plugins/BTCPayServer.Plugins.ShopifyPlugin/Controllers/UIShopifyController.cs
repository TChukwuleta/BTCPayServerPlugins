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
using Microsoft.Extensions.Primitives;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Plugins.Shopify.Models;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIShopifyController : Controller
{
    private readonly ShopifyHostedService _shopifyService;
    private readonly ILogger<UIShopifyController> _logger;
    private readonly ILogger<ShopifyApiClient> _clientLogger;
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
        ILogger<ShopifyApiClient> clientLogger)
    {
        _storeRepo = storeRepo;
        _clientLogger = clientLogger;
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
        if (!storeHasWallet)
        {
            return View(new ShopifySetting
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasWallet = false 
            });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new ShopifySetting();
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
                        var validCreds = vm?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Shopify credentials";
                            return View(vm);
                        }
                        var apiClient = new ShopifyApiClient(_clientFactory, vm.CreateShopifyApiCredentials(), _clientLogger);
                        try
                        {
                            await apiClient.OrdersCount();
                        }
                        catch (ShopifyApiException err)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = $"Invalid Shopify credentials: {err.Message}";
                            return View(vm);
                        }
                        var scopesGranted = await apiClient.CheckScopes();
                        if (!scopesGranted.Contains("read_orders") || !scopesGranted.Contains("write_orders"))
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please grant the private app permissions for read_orders, write_orders.";
                            return View(vm);
                        }
                        vm.IntegratedAt = DateTimeOffset.UtcNow;
                        vm.StoreId = CurrentStore.Id;
                        var webhookResponse = await apiClient.CreateWebhook("orders/create", Url.Action("OrderCreatedWebhook", "UIShopify", new { storeId = vm.StoreId, shopName = vm.ShopName }, Request.Scheme));
                        vm.WebhookId = webhookResponse.Webhook.Id.ToString();
                        vm.ApplicationUserId = GetUserId();
                        vm.StoreName = CurrentStore.StoreName;
                        ctx.Update(vm);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Shopify plugin successfully updated";
                        break;
                    }
                case "ShopifyClearCredentials":
                    {
                        var shopifySetting = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
                        if (shopifySetting != null)
                        {
                            var apiClient = new ShopifyApiClient(_clientFactory, shopifySetting.CreateShopifyApiCredentials(), _clientLogger);
                            await apiClient.RemoveWebhook(shopifySetting.WebhookId);
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
    [HttpGet("~/stores/{storeId}/plugins/shopify/validate/{shopName}")]
    public async Task<IActionResult> ValidateShopifyAccount(string storeId, string shopName)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.ShopName == shopName && c.StoreId == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified. Kindly ensure you have setup Shopify plugin on BTCPay Server");
        }
        return Ok(new { BTCPayStoreId = shopifySetting.StoreId, BTCPayStoreUrl = Request.GetAbsoluteRoot() });
    }

    [AllowAnonymous]
    [HttpPost("stores/{storeId}/plugins/shopify/{shopName}/webhook/order-created")]
    public async Task<IActionResult> OrderCreatedWebhook(string storeId, string shopName)
    {
        try
        {
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync();
            }
            if (!Request.Headers.TryGetValue("X-Shopify-Hmac-SHA256", out StringValues shopifyHmacHeader))
            {
                return BadRequest("Missing HMAC header");
            }
            await using var ctx = _dbContextFactory.CreateContext();
            var shopifySetting = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId && c.ShopName == shopName);
            if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
            {
                return BadRequest("Invalid Shopify BTCPay store specified");
            }
            bool isValid = VerifyWebhookSignature(requestBody, shopifyHmacHeader, shopifySetting.ApiSecret);
            if (!isValid)
            {
                return Unauthorized("Invalid HMAC signature");
            }
            var orderData = JsonConvert.DeserializeObject<dynamic>(requestBody);
            _logger.LogInformation($"Order information: {JsonConvert.SerializeObject(orderData)}");
            Order order = new Order
            {
                ShopName = shopifySetting.ShopName,
                StoreId = shopifySetting.StoreId,
                OrderId = orderData.id,
                FinancialStatus = orderData.financial_status,
                CheckoutId = orderData.checkout_id,
                CheckoutToken = orderData.checkout_token,
                OrderNumber = orderData.order_number,
                FulfilmentStatus = orderData.fulfillment_status
            };
            ctx.Add(order);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred: {ex.Message}");
            return BadRequest();
        }
    }

    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/shopify/orders/{shopName}")]
    public async Task<IActionResult> RetrieveOrderDetails(string storeId, string shopName, string checkoutToken)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.ShopName == shopName && c.StoreId == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified");
        }
        ShopifyApiClient client = new ShopifyApiClient(_clientFactory, shopifySetting.CreateShopifyApiCredentials(), _clientLogger);
        var orders = await client.RetrieveAllOrders();
        if (string.IsNullOrEmpty(checkoutToken))
        {
            orders = orders.Where(c => c.CheckoutToken == checkoutToken).ToList();
        }
        return Ok(ShopifyExtensions.GetShopifyOrderResponse(orders));
    }

    [AllowAnonymous]
    [HttpPost("~/stores/{storeId}/plugins/shopify/{shopName}/create-order")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateShopifyOrderRequest model, string storeId, string shopName)
    {
        if (!shopName.Equals(model.shopName))
        {
            return BadRequest("Shop name mismatch");
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var shopifySetting = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.ShopName == shopName && c.StoreId == storeId);
        if (shopifySetting == null || !shopifySetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid Shopify BTCPay store specified");
        }
        ShopifyApiClient client = new ShopifyApiClient(_clientFactory, shopifySetting.CreateShopifyApiCredentials(), _clientLogger);
        var orderDetails = ctx.Orders.AsNoTracking().FirstOrDefault(c => c.CheckoutToken == model.checkoutToken && c.FinancialStatus.ToLower() == "pending");
        if (orderDetails == null)
        {
            return BadRequest("Invalid checkout token specified");
        }
        ShopifyOrder order = await client.GetOrder(orderDetails.OrderId);
        var store = await _storeRepo.FindStore(shopifySetting.StoreId);
        if (order == null || store == null)
        {
            return NotFound();
        }
        var shopifySearchTerm = $"{SHOPIFY_ORDER_ID_PREFIX}{order.Id}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { shopifySetting.StoreId }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SHOPIFY_ORDER_ID_PREFIX).Any(s => s == order.Id.ToString())).ToArray();

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
                    await _shopifyService.Process(client, order.Id.ToString(), firstInvoiceSettled.Id,
                        firstInvoiceSettled.Currency,
                        firstInvoiceSettled.Price.ToString(CultureInfo.InvariantCulture), true, firstInvoiceSettled.Status.ToString().ToLower());
                }

                return Ok(new
                {
                    invoiceId = firstInvoiceSettled.Id,
                    status = firstInvoiceSettled.Status.ToString().ToLowerInvariant()
                });
            }

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
                ShopName = shopName,
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
            return BadRequest($"An error occurred while trying to create invoice for shopify. {ex.Message}");
        }
    }

    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/btcpay-shopify.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.ShopifySettings.AsNoTracking().FirstOrDefault(c => c.ShopName == storeId);
        if (userStore == null || !userStore.IntegratedAt.HasValue)
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

    private static bool VerifyWebhookSignature(string requestBody, string shopifyHmacHeader, string clientSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
            var hashString = Convert.ToBase64String(hashBytes);
            return hashString.Equals(shopifyHmacHeader, StringComparison.OrdinalIgnoreCase);
        }
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

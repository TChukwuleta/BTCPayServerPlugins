using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using Microsoft.AspNetCore.Http;
using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using System;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Controllers;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Cors;
using BTCPayServer.Payments;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using NicolasDorier.RateLimits;
using System.Globalization;
using BTCPayServer.Plugins.ShopifyPlugin.Helper;
using BTCPayServer.Plugins.ShopifyPlugin.Services;
using BTCPayServer.Plugins.ShopifyPlugin;
using BTCPayServer.Plugins.Shopify.Models;
using System.Net.Http;
using BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;
using BTCPayServer.Client.Models;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Route("~/plugins/stores/{storeId}/shopify")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIShopifyController : Controller
{
    private readonly ShopifyService _shopifyService;
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
        ShopifyService shopifyService,
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
    public BTCPayServer.Data.StoreData CurrentStore => HttpContext.GetStoreData();

    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        return View();
    }

    [HttpGet]
    [Route("~/plugins/stores/{storeId}/shopify")]
    public IActionResult EditShopify()
    {
        var blob = CurrentStore.GetStoreBlob();

        return View(blob.GetShopifySettings());
    }


    [HttpPost("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> EditShopify(string storeId,
            ShopifySettings vm, string command = "")
    {
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

                    // everything ready, proceed with saving Shopify integration credentials
                    shopify.IntegratedAt = DateTimeOffset.Now;

                    // Change this implementation to save to shopify table
                    var blob = CurrentStore.GetStoreBlob();
                    blob.SetShopifySettings(shopify);
                    if (CurrentStore.SetStoreBlob(blob))
                    {
                        await _storeRepo.UpdateStore(CurrentStore);
                    }

                    TempData[WellKnownTempData.SuccessMessage] = "Shopify plugin successfully updated";
                    break;
                }
            case "ShopifyClearCredentials":
                {
                    var blob = CurrentStore.GetStoreBlob();
                    blob.SetShopifySettings(null);
                    if (CurrentStore.SetStoreBlob(blob))
                    {
                        await _storeRepo.UpdateStore(CurrentStore);
                    }

                    TempData[WellKnownTempData.SuccessMessage] = "Shopify plugin credentials cleared";
                    break;
                }
        }

        return RedirectToAction(nameof(EditShopify), new { storeId = CurrentStore.Id });
    }


    [AllowAnonymous]
    [HttpGet("~/plugins/stores/{storeId}/shopify/shopify.js")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        var jsFile = await helper.GetCustomJavascript(storeId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }


    [RateLimitsFilter(ZoneLimits.Shopify, Scope = RateLimitsScope.RemoteAddress)]
    [AllowAnonymous]
    [EnableCors(CorsPolicies.All)]
    [HttpGet("stores/{storeId}/plugins/shopify/{orderId}")]
    public async Task<IActionResult> ShopifyInvoiceEndpoint(
           string storeId, string orderId, decimal amount, bool checkOnly = false)
    {
        var shopifySearchTerm = $"{ShopifyService.SHOPIFY_ORDER_ID_PREFIX}{orderId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { storeId }
        });
        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(ShopifyService.SHOPIFY_ORDER_ID_PREFIX)
                    .Any(s => s == orderId))
            .ToArray();

        var firstInvoiceStillPending =
            matchedExistingInvoices.FirstOrDefault(entity =>
                entity.GetInvoiceState().Status == InvoiceStatus.New);
        if (firstInvoiceStillPending != null)
        {
            return Ok(new
            {
                invoiceId = firstInvoiceStillPending.Id,
                status = firstInvoiceStillPending.Status.ToString().ToLowerInvariant()
            });
        }

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { InvoiceStatus.Processing, InvoiceStatus.Settled }
                    .Contains(
                        entity.GetInvoiceState().Status));

        var store = await _storeRepository.FindStore(storeId);
        var shopify = store?.GetStoreBlob()?.GetShopifySettings();
        ShopifyApiClient client = null;
        ShopifyOrder order = null;
        if (shopify?.IntegratedAt.HasValue is true)
        {
            client = new ShopifyApiClient(_clientFactory, shopify.CreateShopifyApiCredentials());
            order = await client.GetOrder(orderId);
            if (order?.Id is null)
            {
                return NotFound();
            }
        }

        if (firstInvoiceSettled != null)
        {
            //if BTCPay was shut down before the tx managed to get registered on shopify, this will fix it on the next UI load in shopify
            if (client != null && order?.FinancialStatus == "pending" &&
                firstInvoiceSettled.Status != InvoiceStatus.Processing)
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

        if (shopify?.IntegratedAt.HasValue is true)
        {
            if (order?.Id is null ||
                !new[] { "pending", "partially_paid" }.Contains(order.FinancialStatus))
            {
                return NotFound();
            }

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

            return Ok(new { invoiceId = invoice.Id, status = invoice.Status.ToString().ToLowerInvariant() });
        }

        return NotFound();
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

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
using Newtonsoft.Json.Linq;
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
    private readonly IHttpClientFactory _clientFactory;
    private ShopifyHelper helper;
    public UIShopifyController
        (StoreRepository storeRepo,
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
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
        helper = new ShopifyHelper();
        _logger = logger;
    }
    private const string SHOPIFY_ORDER_ID_PREFIX = "shopify-";
    public BTCPayServer.Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new ShopifySetting();
        return View(userStore);
    }

    [HttpPost("~/plugins/stores/{storeId}/shopify")]
    public async Task<IActionResult> Index(string storeId,
            ShopifySetting vm, string command = "")
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
                    shopify.IntegratedAt = DateTimeOffset.Now;
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
        await using var ctx = _dbContextFactory.CreateContext();

        var userStore = ctx.ShopifySettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (userStore == null)
        {
            return BadRequest("Invalid BTCPay store specified");
        }

        var shopifySearchTerm = $"{SHOPIFY_ORDER_ID_PREFIX}{orderId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { storeId }
        });
        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SHOPIFY_ORDER_ID_PREFIX)
                    .Any(s => s == orderId))
            .ToArray();

        var firstInvoiceStillPending =
            matchedExistingInvoices.FirstOrDefault(entity =>
                entity.GetInvoiceState().Status.ToString().Equals("New"));

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
                new[] { InvoiceStatus.Processing.ToString(), InvoiceStatus.Settled.ToString() }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString()));

        var store = await _storeRepo.FindStore(storeId);

        ShopifyApiClient client = null;
        ShopifyOrder order = null;
        if (userStore.IntegratedAt.HasValue)
        {
            client = new ShopifyApiClient(_clientFactory, userStore.CreateShopifyApiCredentials());
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
}

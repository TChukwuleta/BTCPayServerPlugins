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
using System;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Plugins.BigCommercePlugin.Helper;
using BTCPayServer.Controllers;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using Microsoft.Extensions.Logging;
using System.Web;
using Newtonsoft.Json;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Cors;
using BTCPayServer.Payments;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using BTCPayServer.Filters;
using System.Security.Cryptography.X509Certificates;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Route("~/stores/{storeId}/plugins/bigcommerce")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIBigCommerceController : Controller
{
    private readonly HttpClient _client;
    private readonly BigCommerceHelper helper;
    private readonly StoreRepository _storeRepo;
    private readonly BigCommerceService _bigCommerceService;
    private readonly UIInvoiceController _invoiceController;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly ILogger<UIBigCommerceController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BigCommerceDbContextFactory _dbContextFactory;
    public UIBigCommerceController
        (HttpClient client,
        StoreRepository storeRepo,
        BTCPayNetworkProvider networkProvider,
        UIInvoiceController invoiceController,
        BigCommerceService bigCommerceService,
        ILogger<UIBigCommerceController> logger,
        UserManager<ApplicationUser> userManager,
        BigCommerceDbContextFactory dbContextFactory)
    {
        _client = client;
        _storeRepo = storeRepo;
        _userManager = userManager;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
        _bigCommerceService = bigCommerceService;
        helper = new BigCommerceHelper(_client, _bigCommerceService, _dbContextFactory);
        _logger = logger;
    }
    private const string BIGCOMMERCE_ORDER_ID_PREFIX = "BigCommerce-";
    public StoreData CurrentStore => HttpContext.GetStoreData();


    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var storeData = await _storeRepo.FindStore(storeId);
        if (storeData == null)
            return NotFound();

        if (TempData["SuccessMessage"] != null)
        {
            ViewBag.SuccessMessage = TempData["SuccessMessage"];
            TempData.Remove("SuccessMessage");
        }
        if (TempData["ErrorMessage"] != null)
        {
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            TempData.Remove("ErrorMessage");
        }

        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        if (!storeHasWallet)
        {
            return View(new InstallBigCommerceViewModel
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasStore = false
            });
        }
        var bigCommerceStore = ctx.BigCommerceStores.SingleOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            bigCommerceStore = new BigCommerceStore
            {
                StoreId = CurrentStore.Id,
                StoreName = CurrentStore.StoreName,
                ApplicationUserId = GetUserId(),
                RedirectUrl = Url.Action("Install", "UIBigCommerce", null, Request.Scheme)
            };
            ctx.Add(bigCommerceStore);
            await ctx.SaveChangesAsync();
        }
        return View(new InstallBigCommerceViewModel
        {
            ClientId = bigCommerceStore.ClientId,
            ClientSecret = bigCommerceStore.ClientSecret,
            AuthCallBackUrl = Url.Action("Install", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            LoadCallbackUrl = Url.Action("Load", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            UninstallCallbackUrl = Url.Action("Uninstall", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            CheckoutScriptUrl = Url.Action("GetBtcPayJavascript", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            StoreName = bigCommerceStore.StoreName
        });
    }

    [HttpPost("~/plugins/bigcommerce/create")]
    public async Task<IActionResult> Create(InstallBigCommerceViewModel model)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (userStore is null)
            return NotFound();
        var hasConflictingStore = ctx.BigCommerceStores
        .Where(store => store.StoreId != CurrentStore.Id)
        .Any(store => store.ClientId == model.ClientId || store.ClientSecret == model.ClientSecret);
        if (hasConflictingStore)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot create BigCommerce store as a store with the same Client ID or Client Secret already exists";
            return RedirectToAction(nameof(Index), "UIBigCommerce", new { storeId = CurrentStore.Id });
        }
        userStore.ClientId = model.ClientId;
        userStore.ClientSecret = model.ClientSecret;
        ctx.Update(userStore);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Big commerce record saved successfully";
        return RedirectToAction(nameof(Index), "UIBigCommerce", new { storeId = CurrentStore.Id });
    }


    [AllowAnonymous]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/auth/install")]
    public async Task<IActionResult> Install(string storeId, [FromQuery] string account_uuid, [FromQuery] string code, [FromQuery] string context, [FromQuery] string scope)
    {
        try
        {
            code = HttpUtility.UrlDecode(code);
            context = HttpUtility.UrlDecode(context);
            scope = HttpUtility.UrlDecode(scope);

            await using var ctx = _dbContextFactory.CreateContext();
            var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
            if (bigCommerceStore == null)
            {
                return BadRequest("Invalid request");
            }
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(context) || string.IsNullOrEmpty(scope))
            {
                return BadRequest("Missing required query parameters.");
            }
            var installRequest = new InstallBigCommerceApplicationRequestModel
            {
                ClientId = bigCommerceStore.ClientId,
                ClientSecret = bigCommerceStore.ClientSecret,
                Code = code,
                RedirectUrl = Url.Action("Install", "UIBigCommerce", new { storeId }, Request.Scheme),
                Context = context,
                Scope = scope
            };
            var responseCall = await _bigCommerceService.InstallApplication(installRequest);
            if (!responseCall.Success)
            {
                _logger.LogError($"{responseCall.Content}");
                return BadRequest(responseCall.Content);
            }
            var bigCommerceStoreDetails = JsonConvert.DeserializeObject<InstallApplicationResponseModel>(responseCall.Content);
            bigCommerceStore.AccessToken = bigCommerceStoreDetails.access_token;
            bigCommerceStore.Scope = bigCommerceStoreDetails.scope;
            bigCommerceStore.StoreHash = bigCommerceStoreDetails.context;
            bigCommerceStore.BigCommerceUserEmail = bigCommerceStoreDetails.user.email;
            bigCommerceStore.BigCommerceUserId = bigCommerceStoreDetails.user.id.ToString();
            ctx.Update(bigCommerceStore);
            await ctx.SaveChangesAsync();
            bigCommerceStore = await helper.UploadCheckoutScript(bigCommerceStore, Url.Action("GetBtcPayJavascript", "UIBigCommerce", new { storeId }, Request.Scheme));
            ctx.Update(bigCommerceStore);
            await ctx.SaveChangesAsync();
            return Content(BigCommerceIframeResponse(bigCommerceStore), "text/html");
        }
        catch (Exception)
        {
            return BadRequest("An error occurred while completing Big commerce installation");
        }
    }


    [AllowAnonymous]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/auth/load")]
    public async Task<IActionResult> Load(string storeId, [FromQuery] string signed_payload_jwt)
    {
        _logger.LogInformation(signed_payload_jwt);
        if (string.IsNullOrEmpty(signed_payload_jwt))
        {
            return BadRequest("Missing signed_payload_jwt parameter");
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            return BadRequest("Invalid request");
        }
        var claims = helper.DecodeJwtPayload(signed_payload_jwt);
        if (!helper.ValidateClaims(bigCommerceStore, claims))
        {
            return BadRequest("Invalid signed_payload_jwt parameter");
        }
        return Content(BigCommerceIframeResponse(bigCommerceStore), "text/html");
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/auth/uninstall")]
    public async Task<IActionResult> Uninstall(string storeId, [FromQuery] string signed_payload_jwt)
    {
        if (string.IsNullOrEmpty(signed_payload_jwt))
        {
            return BadRequest("Missing signed_payload_jwt parameter");
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            return BadRequest("Invalid request");
        }
        var claims = helper.DecodeJwtPayload(signed_payload_jwt);
        if (!helper.ValidateClaims(bigCommerceStore, claims))
        {
            return BadRequest("Invalid signed_payload_jwt parameter");
        }
        ctx.Remove(bigCommerceStore);
        await ctx.SaveChangesAsync();
        return Ok("Big commerce store uninstalled successfully");
    }


    [AllowAnonymous]
    [HttpPost("~/stores/{storeId}/plugins/bigcommerce/create-order")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateBigCommerceStoreRequest requestModel)
    {
        try
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var exisitngStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == requestModel.storeId);
            if (exisitngStore == null)
            {
                return BadRequest("Cannot create big commerce order. Invalid store Id");
            }
            var createOrder = await _bigCommerceService.CheckoutOrderAsync(exisitngStore.StoreHash, requestModel.cartId, exisitngStore.AccessToken);
            if (createOrder == null)
            {
                return BadRequest($"An error occurred while creating order. {JsonConvert.SerializeObject(createOrder)}");
            }
            string bgOrderId = $"{BIGCOMMERCE_ORDER_ID_PREFIX}{createOrder.data.id}";
            InvoiceMetadata metadata = new InvoiceMetadata
            {
                OrderId = bgOrderId,
                BuyerEmail = requestModel.email
            };

            var store = await _storeRepo.FindStore(exisitngStore.StoreId);
            var result = await _invoiceController.CreateInvoiceCoreRaw(new Client.Models.CreateInvoiceRequest()
            {
                Amount = requestModel.total,
                Currency = requestModel.currency,
                Metadata = metadata.ToJObject(),
            }, store, HttpContext.Request.GetAbsoluteRoot());
            var entity = new Transaction
            {
                ClientId = exisitngStore.ClientId,
                StoreHash = exisitngStore.StoreHash,
                StoreId = exisitngStore.StoreId,
                OrderId = bgOrderId,
                InvoiceId = result.Id,
                TransactionStatus = TransactionStatus.Pending,
                InvoiceStatus = Client.Models.InvoiceStatus.New.ToString()
            };
            ctx.Add(entity);
            await ctx.SaveChangesAsync();
            return Ok(new
            {
                id = result.Id,
                orderId = createOrder.data.id.ToString(),
                Message = "Order created and invoice generated successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"An error occurred while trying to create order for Big Commerce. {ex.Message}");
        }
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/btcpay-bc.js")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        var jsFile = await helper.GetCustomJavascript(storeId, Request.GetAbsoluteRoot());
        if (!jsFile.Success)
        {
            return BadRequest(jsFile.Content);
        }
        return Content(jsFile.Content, "text/javascript");
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/modal/btcpay.js")]
    public async Task<IActionResult> GetBtcPayModalJavascript(string storeId)
    {
        var jsFile = await helper.GetBtcpayCustomJavascriptModal(storeId);
        if (!jsFile.Success)
        {
            return BadRequest(jsFile.Content);
        }
        return Content(jsFile.Content, "text/javascript");
    }

    private static Dictionary<PaymentMethodId, JToken> GetPaymentMethodConfigs(StoreData storeData, bool onlyEnabled = false)
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

    private string BigCommerceIframeResponse(BigCommerceStore bigCommerceStore)
    {
        return $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='utf-8' />
                <title>BTCPay Plugin Configuration</title>
                <meta name='viewport' content='width=device-width, initial-scale=1.0' />
                <style>
                    table {{
                        width: 60%;
                        border-collapse: collapse;
                        margin: 20px auto;
                    }}
                    th, td {{
                        border: 1px solid #ddd;
                        padding: 8px;
                        text-align: left;
                    }}
                    th {{
                        background-color: #f2f2f2;
                    }}
                </style>
            </head>
            <body>
                <h2 style='text-align: center;'>BTCPay Plugin Configuration</h2>
                <table>
                    <tr>
                        <th>BTCPay Server Store Name</th>
                        <td>{bigCommerceStore.StoreName}</td>
                    </tr>
                    <tr>
                        <th>Auth Callback URL</th>
                        <td>{Url.Action("Install", "UIBigCommerce", new { storeId = bigCommerceStore.StoreId }, Request.Scheme)}</td>
                    </tr>
                    <tr>
                        <th>Load Callback URL</th>
                        <td>{Url.Action("Load", "UIBigCommerce", new { storeId = bigCommerceStore.StoreId }, Request.Scheme)}</td>
                    </tr>
                    <tr>
                        <th>Uninstall Callback URL</th>
                        <td>{Url.Action("Uninstall", "UIBigCommerce", new { storeId = bigCommerceStore.StoreId }, Request.Scheme)}</td>
                    </tr>
                </table>
            </body>
            </html>";
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

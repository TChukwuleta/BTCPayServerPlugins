using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Filters;
using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Helper;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Route("~/stores/{storeId}/plugins/bigcommerce")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
public class UIBigCommerceController(HttpClient client,
        StoreRepository storeRepo,
        UIInvoiceController invoiceController,
        BigCommerceService bigCommerceService,
        ILogger<UIBigCommerceController> logger,
        UserManager<ApplicationUser> userManager,
        BigCommerceDbContextFactory dbContextFactory) : Controller
{
    private const string BIGCOMMERCE_ORDER_ID_PREFIX = "BigCommerce-";
    BigCommerceHelper helper = new BigCommerceHelper(client, bigCommerceService, dbContextFactory);
    public StoreData CurrentStore => HttpContext.GetStoreData();


    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId)) return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

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
        var bigCommerceStore = ctx.BigCommerceStores.SingleOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            bigCommerceStore = new BigCommerceStore
            {
                StoreId = CurrentStore.Id,
                StoreName = CurrentStore.StoreName,
                ApplicationUserId = GetUserId(),
                RedirectUrl = Url.Action(nameof(Install), "UIBigCommerce", null, Request.Scheme)
            };
            ctx.Add(bigCommerceStore);
            await ctx.SaveChangesAsync();
        }
        return View(new InstallBigCommerceViewModel
        {
            StoreId = CurrentStore.Id,
            ClientId = bigCommerceStore.ClientId,
            ClientSecret = bigCommerceStore.ClientSecret,
            AuthCallBackUrl = Url.Action(nameof(Install), "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            LoadCallbackUrl = Url.Action(nameof(Load), "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            UninstallCallbackUrl = Url.Action(nameof(Uninstall), "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            CheckoutScriptUrl = Url.Action(nameof(GetBtcPayJavascript), "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            StoreName = bigCommerceStore.StoreName
        });
    }


    [HttpPost("~/stores/{storeId}/plugins/bigcommerce/create")]
    public async Task<IActionResult> Create(InstallBigCommerceViewModel model)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var userStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (userStore is null) return NotFound();

        var hasConflictingStore = ctx.BigCommerceStores.Where(store => store.StoreId != CurrentStore.Id)
            .Any(store => store.ClientId == model.ClientId || store.ClientSecret == model.ClientSecret);
        if (hasConflictingStore)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot create BigCommerce store. A different store is using the client credentials";
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
        userStore.ClientId = model.ClientId;
        userStore.ClientSecret = model.ClientSecret;
        ctx.Update(userStore);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Big commerce record saved successfully";
        return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
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
            await using var ctx = dbContextFactory.CreateContext();
            var (bigCommerceStore, notFound) = await FindStoreOrError(ctx, storeId, "Invalid request. Kindly confirm that your Client Id and secret are configured on your BTCPay instance");
            if (notFound != null)
                return notFound;

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(context) || string.IsNullOrEmpty(scope))
            {
                return BadRequest("Missing required query parameters");
            }
            var responseCall = await bigCommerceService.InstallApplication(new InstallBigCommerceApplicationRequestModel
            {
                ClientId = bigCommerceStore.ClientId,
                ClientSecret = bigCommerceStore.ClientSecret,
                Code = code,
                RedirectUrl = Url.Action(nameof(Install), "UIBigCommerce", new { storeId }, Request.Scheme),
                Context = context,
                Scope = scope
            });
            if (!responseCall.Success)
            {
                logger.LogError("BigCommerce install failed for store {StoreId}: {Content}", storeId, responseCall.Content);
                return BadRequest(responseCall.Content);
            }
            var bigCommerceStoreDetails = JsonConvert.DeserializeObject<InstallApplicationResponseModel>(responseCall.Content);
            bigCommerceStore.AccessToken = bigCommerceStoreDetails.access_token;
            bigCommerceStore.Scope = bigCommerceStoreDetails.scope;
            bigCommerceStore.StoreHash = bigCommerceStoreDetails.context;
            bigCommerceStore.BigCommerceUserEmail = bigCommerceStoreDetails.user.email;
            bigCommerceStore.BigCommerceUserId = bigCommerceStoreDetails.user.id.ToString();
            bigCommerceStore = await helper.UploadCheckoutScript(bigCommerceStore, Url.Action(nameof(GetBtcPayJavascript), "UIBigCommerce", new { storeId }, Request.Scheme));
            ctx.Update(bigCommerceStore);
            await ctx.SaveChangesAsync();
            return Content(BigCommerceIframeResponse(bigCommerceStore), "text/html");
        }
        catch (Exception ex)
        {
            return BadRequest($"An error occurred while completing Big commerce installation. {JsonConvert.SerializeObject(ex.Message)}");
        }
    }


    [AllowAnonymous]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/auth/load")]
    public async Task<IActionResult> Load(string storeId, [FromQuery] string signed_payload_jwt)
    {
        if (string.IsNullOrEmpty(signed_payload_jwt))
        {
            return BadRequest("Missing JWT parameter. Kindly refresh this page");
        }
        await using var ctx = dbContextFactory.CreateContext();
        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            return BadRequest("Invalid request");
        }
        var claims = helper.DecodeJwtPayload(signed_payload_jwt);
        if (!helper.ValidateClaims(bigCommerceStore, claims))
        {
            return BadRequest("Invalid JWT parameter. Kindly refresh this page");
        }
        return Content(BigCommerceIframeResponse(bigCommerceStore), "text/html");
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/auth/uninstall")]
    public async Task<IActionResult> Uninstall(string storeId, [FromQuery] string signed_payload_jwt)
    {
        if (string.IsNullOrEmpty(signed_payload_jwt))
        {
            return BadRequest("Missing JWT parameter. Kindly refresh this page");
        }
        await using var ctx = dbContextFactory.CreateContext();
        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null) return BadRequest("Invalid request");

        var claims = helper.DecodeJwtPayload(signed_payload_jwt);
        if (!helper.ValidateClaims(bigCommerceStore, claims))
        {
            return BadRequest("Invalid JWT parameter. Kindly refresh this page");
        }
        ctx.Remove(bigCommerceStore);
        await ctx.SaveChangesAsync();
        return Ok("Big commerce store uninstalled successfully");
    }


    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("~/stores/{storeId}/plugins/bigcommerce/create-order")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateBigCommerceStoreRequest requestModel)
    {
        try
        {
            await using var ctx = dbContextFactory.CreateContext();
            var existingStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == requestModel.storeId);
            if (existingStore == null)
                return BadRequest("Cannot create big commerce order. Invalid store Id");

            var createOrder = await bigCommerceService.CheckoutOrder(existingStore.StoreHash, requestModel.cartId, existingStore.AccessToken);
            if (createOrder == null)
                return BadRequest("An error occurred while creating the order on BigCommerce.");

            string bgOrderId = $"{BIGCOMMERCE_ORDER_ID_PREFIX}{createOrder.data.id}";

            if (ctx.Transactions.Any(t => t.OrderId == bgOrderId))
                return BadRequest("An invoice already exists for this order.");

            var orderDetails = await bigCommerceService.GetOrder(createOrder.data.id, existingStore.StoreHash, existingStore.AccessToken);
            if (orderDetails == null || !decimal.TryParse(orderDetails.total_inc_tax, NumberStyles.Any, CultureInfo.InvariantCulture, out var authoritativeTotal) ||
                authoritativeTotal <= 0 || string.IsNullOrEmpty(orderDetails.currency_code))
            {
                logger.LogError("Could not verify BigCommerce order {OrderId} total for store {StoreId}", createOrder.data.id, existingStore.StoreId);
                return BadRequest("Could not verify the order's total with BigCommerce.");
            }

            var metadata = new InvoiceMetadata { OrderId = bgOrderId, BuyerEmail = requestModel.email };
            var store = await storeRepo.FindStore(existingStore.StoreId);
            var result = await invoiceController.CreateInvoiceCoreRaw(new Client.Models.CreateInvoiceRequest()
            {
                Amount = authoritativeTotal,
                Currency = orderDetails.currency_code,
                Metadata = metadata.ToJObject(),
            }, store, HttpContext.Request.GetAbsoluteRoot());

            ctx.Add(new Transaction
            {
                ClientId = existingStore.ClientId,
                StoreHash = existingStore.StoreHash,
                StoreId = existingStore.StoreId,
                OrderId = bgOrderId,
                InvoiceId = result.Id,
                TransactionStatus = TransactionStatus.Pending,
                InvoiceStatus = Client.Models.InvoiceStatus.New.ToString()
            });
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
            logger.LogError(ex, "BigCommerce create-order failed for store {StoreId}", requestModel?.storeId);
            return BadRequest($"An error occurred while trying to create order for Big Commerce. {ex.Message}");
        }
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/btcpay-bc.js")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var bcStore = await ctx.BigCommerceStores.FirstOrDefaultAsync(c => c.StoreId == storeId);
        if (bcStore == null) 
            return BadRequest("Invalid store Id specified");

        var jsVariables = $"var BTCPAYSERVER_URL = '{Request.GetAbsoluteRoot()}'; var BTCPAYSERVER_STORE_ID = '{storeId}'; var STORE_HASH = '{bcStore.StoreHash}';";
        var js = jsVariables + Environment.NewLine + helper.GetEmbeddedResourceContent("Resources.js.btcpay-bc.js");
        return Content(js, "text/javascript");
    }


    [AllowAnonymous]
    [HttpGet("~/stores/{storeId}/plugins/bigcommerce/modal/btcpay.js")]
    public async Task<IActionResult> GetBtcPayModalJavascript(string storeId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var bcStore = await ctx.BigCommerceStores.FirstOrDefaultAsync(c => c.StoreId == storeId);
        if (bcStore == null) 
            return BadRequest("Invalid store Id specified");

        var js = helper.GetEmbeddedResourceContent("Resources.js.btcpay.js");
        return Content(js, "text/javascript");
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

    private string GetUserId() => userManager.GetUserId(User);

    private async Task<(BigCommerceStore? Store, IActionResult? Error)> FindStoreOrError(BigCommerceDbContext ctx, string storeId, string notFoundMessage)
    {
        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
        return bigCommerceStore == null ? (null, BadRequest(notFoundMessage)) : (bigCommerceStore, null);
    }
}

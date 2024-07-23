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
using System.Threading;
using BTCPayServer.Models.InvoicingModels;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Plugins.BigCommercePlugin.Helper;
using BTCPayServer.Controllers;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Web;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Route("~/plugins/{storeId}/bigcommerce")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIBigCommerceController : Controller
{
    private readonly ILogger<UIBigCommerceController> _logger;  
    private readonly BigCommerceService _bigCommerceService;
    private readonly UIInvoiceController _invoiceController;
    private readonly BigCommerceDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private BigCommerceHelper helper;
    public UIBigCommerceController
        (UIInvoiceController invoiceController,
        ILogger<UIBigCommerceController> logger,
        BigCommerceService bigCommerceService,
        UserManager<ApplicationUser> userManager,
        BigCommerceDbContextFactory dbContextFactory)
    {
        _logger = logger;
        _userManager = userManager;
        _invoiceController = invoiceController;
        _dbContextFactory = dbContextFactory;
        _bigCommerceService = bigCommerceService;
        helper = new BigCommerceHelper(_bigCommerceService, _dbContextFactory);
    }

    public const string BIGCOMMERCE_ORDER_ID_PREFIX = "BigCommerce-";
    public StoreData CurrentStore => HttpContext.GetStoreData();


    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var bigCommerceStore = ctx.BigCommerceStores.SingleOrDefault(c => c.StoreId == storeId);
        if (bigCommerceStore == null)
        {
            return RedirectToAction(nameof(Create), "UIBigCommerce");
        }
        TempData[WellKnownTempData.SuccessMessage] = "Big commerce store details retrieved successfully";
        return View(new InstallBigCommerceViewModel
        {
            ClientId = bigCommerceStore.ClientId,
            ClientSecret = bigCommerceStore.ClientSecret,
            AuthCallBackUrl = Url.Action("Install", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            LoadCallbackUrl = Url.Action("Load", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            UninstallCallbackUrl = Url.Action("Uninstall", "UIBigCommerce", new { storeId = CurrentStore.Id }, Request.Scheme),
            StoreName = bigCommerceStore.StoreName
        });
    }


    [HttpGet("~/plugins/bigcommerce/create")]
    public IActionResult Create()
    {
        if (CurrentStore is null)
            return NotFound();

        return View(new InstallBigCommerceViewModel());
    }


    [HttpPost("~/plugins/bigcommerce/create")]
    public async Task<IActionResult> Create(InstallBigCommerceViewModel model)
    {
        if (CurrentStore is null)
            return NotFound();

        string userId = GetUserId();

        await using var ctx = _dbContextFactory.CreateContext();
        var exisitngStores = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (exisitngStores != null)
        {
            ReturnFailedMessageStatus($"Cannot create big commerce store as there is a store that has currently been installed");
            return RedirectToAction(nameof(Create), "UIBigCommerce");
        }
        var userBigCommerceStores = ctx.BigCommerceStores.Where(store => store.ApplicationUserId == userId).ToList();
        if (userBigCommerceStores.Exists(store => store.ClientId == model.ClientId || store.ClientSecret == model.ClientSecret))
        {
            ModelState.AddModelError(nameof(model.ClientSecret), "Cannot create BigCommerce store as a store with the same Client ID or Client Secret already exists.");
            return RedirectToAction(nameof(Create), "UIBigCommerce");
        }

        var callbackUrl = Url.Action("Install", "UIBigCommerce", null, Request.Scheme);
        var entity = new BigCommerceStore
        {
            StoreId = CurrentStore.Id,
            ClientId = model.ClientId,
            RedirectUrl = callbackUrl,
            ClientSecret = model.ClientSecret,
            StoreName = CurrentStore.StoreName,
            ApplicationUserId = userId
        };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();
        ReturnSuccessMessageStatus($"Big commerce store details saved successfully.");
        return RedirectToAction(nameof(Index), "UIBigCommerce");
    }


    [AllowAnonymous]
    [HttpGet("~/plugins/{storeId}/bigcommerce/auth/install")]
    public async Task<IActionResult> Install(string storeId, [FromQuery] string account_uuid, [FromQuery] string code, [FromQuery] string context, [FromQuery] string scope)
    {
        account_uuid = HttpUtility.UrlDecode(account_uuid);
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
        var responseCall = await _bigCommerceService.InstallApplication(new InstallBigCommerceApplicationRequestModel
        {
            ClientId = bigCommerceStore.ClientId,
            ClientSecret = bigCommerceStore.ClientSecret,
            Code = code,
            RedirectUrl = Url.Action("Install", "UIBigCommerce", new { storeId }, Request.Scheme),
            //RedirectUrl = "https://01c1-102-88-82-88.ngrok-free.app/plugins/7zFr8vWCHQpWXnobdZdjiX8AcAG56fspdjinLYXuyLbi/bigcommerce/auth/install",
            Context = context,
            Scope = scope
        });
        if (!responseCall.success)
        {
            return BadRequest(responseCall.content);
        }
        var bigCommerceStoreDetails = Newtonsoft.Json.JsonConvert.DeserializeObject<InstallApplicationResponseModel>(responseCall.content);
        bigCommerceStore.AccessToken = bigCommerceStoreDetails.access_token;
        bigCommerceStore.Scope = bigCommerceStoreDetails.scope;
        bigCommerceStore.StoreHash = bigCommerceStoreDetails.context;
        bigCommerceStore.BigCommerceUserEmail = bigCommerceStoreDetails.user.email;
        bigCommerceStore.BigCommerceUserId = bigCommerceStoreDetails.user.id.ToString();

        bigCommerceStore = await helper.UploadCheckoutScript(bigCommerceStore, Url.Action("GetBtcPayJavascript", "UIBigCommerce", new { storeId }, Request.Scheme));

        ctx.Update(bigCommerceStore);
        await ctx.SaveChangesAsync();
        return Ok("Big commerce store installation was successful");
    }


    [AllowAnonymous]
    [HttpGet("~/plugins/{storeId}/bigcommerce/auth/load")]
    public async Task<IActionResult> Load(string storeId, [FromQuery] string signed_payload_jwt)
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
        try
        {
            var claims = helper.DecodeJwtPayload(signed_payload_jwt);
            if (!helper.ValidateClaims(bigCommerceStore, claims))
            {
                return BadRequest("Invalid signed_payload_jwt parameter");
            }

            return Redirect("https://bigcommerce.btcpay.tech/");
            //return View();
        }
        catch (Exception ex)
        {
            return BadRequest($"Invalid token: {ex.Message}");
        }
    }


    [AllowAnonymous]
    [HttpGet("~/plugins/{storeId}/bigcommerce/auth/uninstall")]
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
        try
        {
            var claims = helper.DecodeJwtPayload(signed_payload_jwt);
            if (!helper.ValidateClaims(bigCommerceStore, claims))
            {
                return BadRequest("Invalid signed_payload_jwt parameter");
            }
            ctx.Remove(bigCommerceStore);
            await ctx.SaveChangesAsync();

            return Ok("Big commerce store uninstalled successfully.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Invalid token: {ex.Message}");
        }
    }


    [AllowAnonymous]
    [HttpPost("~/plugins/{storeId}/bigcommerce/create-order")]
    public async Task<IActionResult> CreateOrder(CreateBigCommerceStoreRequest requestModel)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var exisitngStores = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == requestModel.storeId);
        if (exisitngStores == null)
        {
            return BadRequest("Cannot create big commerce order. Invalid store Id");
        }
        var createOrder = await _bigCommerceService.CreateOrderAsync(exisitngStores.StoreHash, requestModel.cartId, exisitngStores.AccessToken);
        if (createOrder == null)
        {
            return BadRequest("An error occurred while creating order");
        }

        string bgOrderId = $"{BIGCOMMERCE_ORDER_ID_PREFIX}{createOrder.data.id}";
        var invoiceResult = await _invoiceController.CreateInvoice(new CreateInvoiceModel
        {
            Amount = requestModel.total,
            Currency = requestModel.currency,
            StoreId = exisitngStores.StoreId,
            BuyerEmail = requestModel.email,
            OrderId = bgOrderId,
            Metadata = createOrder.meta.ToString()
        }, new CancellationToken());


        // Find a way to get invoiceId and orderId from this

        /*TempData[WellKnownTempData.SuccessMessage] = $"Invoice {result.Id} just created!";
        CreatedInvoiceId = result.Id;
        return RedirectToAction(nameof(Invoice), new { storeId = result.StoreId, invoiceId = result.Id });*/

        var entity = new Transaction
        {
            ClientId = exisitngStores.ClientId,
            StoreHash = exisitngStores.StoreHash,
            StoreId = exisitngStores.StoreId,
            OrderId = bgOrderId,
            InvoiceId = "",
            TransactionStatus = TransactionStatus.Pending,
            InvoiceStatus = BTCPayServer.Services.Invoices.InvoiceStatusLegacy.New
        };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();

        return Ok(new
        {
            id = "",
            orderId = createOrder.data.id.ToString(),
            Message = "Order created and invoice generated successfully",
            InvoiceResult = invoiceResult
        });
     }

    [AllowAnonymous]
    [HttpGet("~/plugins/{storeId}/bigcommerce/btcpay-bc.js")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        var jsFile = await helper.GetCustomJavascript(storeId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }


    private void ReturnSuccessMessageStatus(string message)
    {
        TempData.SetStatusMessageModel(new StatusMessageModel()
        {
            Message = message,
            Html = message,
            AllowDismiss = true,
            Severity = StatusMessageModel.StatusSeverity.Success
        });
    }

    private void ReturnFailedMessageStatus(string message)
    {
        TempData.SetStatusMessageModel(new StatusMessageModel()
        {
            Message = message,
            Severity = StatusMessageModel.StatusSeverity.Error
        });
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using BTCPayServer.Services.Rates;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using Microsoft.AspNetCore.Http;
using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.BigCommercePlugin.Data.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.BigCommercePlugin;

[Route("~/plugins/storegenerator")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIBigCommerceController : Controller
{
    private readonly ILogger<UIBigCommerceController> _logger;
    private readonly BigCommerceService _bigCommerceService;
    private readonly BigCommerceDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIBigCommerceController
        (BigCommerceDbContextFactory dbContextFactory,
        BigCommerceService bigCommerceService,
        UserManager<ApplicationUser> userManager, 
        ILogger<UIBigCommerceController> logger)
    {
        _logger = logger;
        _userManager = userManager;
        _dbContextFactory = dbContextFactory;
        _bigCommerceService = bigCommerceService;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    // GET
    [HttpGet("~/plugins/{storeId}/bigcommerce/index")]
    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var bigCommerceStore = ctx.BigCommerceStores.SingleOrDefault(c => c.StoreId == CurrentStore.Id);
        if (bigCommerceStore == null)
        {
            return RedirectToAction(nameof(Create), new { storeId = CurrentStore.Id });
        }
        // Have a default view..
        return View(new InstallBigCommerceViewModel());
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

        await using var ctx = _dbContextFactory.CreateContext();

        var exisitngStores = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (exisitngStores != null)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Cannot create big commerce store as there is a store that has currently been installed",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(Create));
            // Return a existing store error
        }
        var callbackUrl = Url.Action("Install", "UIBigCommerce", null, Request.Scheme);
        var entity = new BigCommerceStore
        {
            StoreId = CurrentStore.Id,
            ClientId = model.ClientId,
            RedirectUrl = callbackUrl,
            ClientSecret = model.ClientSecret,
            StoreName = CurrentStore.StoreName,
            ApplicationUserId = GetUserId()
        };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();

        TempData.SetStatusMessageModel(new StatusMessageModel
        {
            Severity = StatusMessageModel.StatusSeverity.Success,
            AllowDismiss = false,
            Html = $"Big commerce store details saved successfully. Kindly include the following url as callback in your Big Commerce store: {callbackUrl}"
        });
        return View(new InstallBigCommerceViewModel());
    }


    [HttpGet("~/plugins/bigcommerce/auth/install")]
    public async Task<IActionResult> Install([FromQuery] string code, [FromQuery] string context, [FromQuery] string scope)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
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
            RedirectUrl = bigCommerceStore.RedirectUrl,
            Context = context,
            Scope = scope
        });
        if (!responseCall.success)
        {
            return BadRequest(responseCall.content);
        }
        var bigCommerceStoreDetails = JsonConvert.DeserializeObject<InstallApplicationResponseModel>(responseCall.content);
        bigCommerceStore.AccessToken = bigCommerceStoreDetails.access_token;
        bigCommerceStore.Scope = bigCommerceStoreDetails.scope;
        bigCommerceStore.StoreHash = bigCommerceStoreDetails.context;
        bigCommerceStore.BigCommerceUserEmail = bigCommerceStoreDetails.user.email;
        bigCommerceStore.BigCommerceUserId = bigCommerceStoreDetails.user.id.ToString();

        ctx.Update(bigCommerceStore); 
        await ctx.SaveChangesAsync();
        return Ok("Big commerce store installation was successful");
    }

    [HttpPost("~/plugins/bigcommerce/auth/uninstall")]
    public async Task<IActionResult> Uninstall()
    {
        if (CurrentStore is null)
            return NotFound();

        var storeHash = HttpContext.Session.GetString("store_hash");
        if (string.IsNullOrEmpty(storeHash))
        {
            return BadRequest("Store hash not found in session.");
        }
        await using var ctx = _dbContextFactory.CreateContext();

        var bigCommerceStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.StoreHash == storeHash);
        if (bigCommerceStore == null)
        {
            return NotFound("Setting not found for the given store hash.");
        }
        ctx.Remove(bigCommerceStore);
        await ctx.SaveChangesAsync();

        HttpContext.Session.Clear();
        return Ok("Big commerce store uninstalled successfully");
    }


    private string GetUserId() => _userManager.GetUserId(User);
}

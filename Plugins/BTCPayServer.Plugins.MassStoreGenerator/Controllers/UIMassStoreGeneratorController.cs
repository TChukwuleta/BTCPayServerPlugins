using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.MassStoreGenerator.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using BTCPayServer.Services.Rates;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.MassStoreGenerator.Helper;
using BTCPayServer.Controllers;

namespace BTCPayServer.Plugins.Template;

[Route("~/plugins/storesgenerator")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIMassStoreGeneratorController : Controller
{
    private readonly ILogger<UIMassStoreGeneratorController> _logger;
    private readonly RateFetcher _rateFactory;
    private readonly StoreRepository _storeRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIMassStoreGeneratorController
        (RateFetcher rateFactory,
        StoreRepository storeRepository,
        UserManager<ApplicationUser> userManager, ILogger<UIMassStoreGeneratorController> logger, IAuthorizationService authorizationService)
    {
        _logger = logger;
        _userManager = userManager;
        _rateFactory = rateFactory;
        _storeRepository = storeRepository;
        _authorizationService = authorizationService;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    // GET
    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null)
            return NotFound();

        var hasPermission = await _authorizationService.AuthorizeAsync(User, CurrentStore.Id, Policies.CanModifyStoreSettingsUnscoped);
        CreateStoreViewModel vm = new CreateStoreViewModel
        {
            HasStoreCreationPermission = hasPermission.Succeeded,
            DefaultCurrency = StoreBlobHelper.StandardDefaultCurrency,
            Exchanges = GetExchangesSelectList(null)
        };
        return View(vm);
    }


    [HttpPost("~/create")]
    public async Task<IActionResult> Create(List<CreateStoreViewModel> model)
    {
        if (CurrentStore is null)
            return NotFound();

        string userId = GetUserId();

        foreach (var vm in model)
        {
            var store = new StoreData { StoreName = vm.Name };
            var blob = store.GetStoreBlob();
            blob.DefaultCurrency = vm.DefaultCurrency;
            blob.PreferredExchange = vm.PreferredExchange;
            store.SetStoreBlob(blob);
            await _storeRepository.CreateStore(userId, store);
        }
        TempData[WellKnownTempData.SuccessMessage] = "Stores successfully created";
        return RedirectToAction(nameof(UIStoresController.Index), "UIStores", new { storeId = CurrentStore.Id });
    }

    private string GetUserId() => _userManager.GetUserId(User);

    private SelectList GetExchangesSelectList(string selected)
    {
        var exchanges = _rateFactory.RateProviderFactory
            .AvailableRateProviders
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var chosen = exchanges.Find(f => f.Id == selected) ?? exchanges[0];
        return new SelectList(exchanges, nameof(chosen.Id), nameof(chosen.DisplayName), chosen.Id);
    }
}

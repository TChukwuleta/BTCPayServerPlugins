using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.MassStoreGenerator.Helper;
using BTCPayServer.Plugins.MassStoreGenerator.ViewModels;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Plugins.MassStoreGenerator;

[Route("~/plugins/{storeId}/storesgenerator/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[AutoValidateAntiforgeryToken]
public class UIMassStoreGeneratorController(RateFetcher rateFactory, StoreRepository storeRepository,
        UserManager<ApplicationUser> userManager, IAuthorizationService authorizationService) : Controller
{
    public StoreData CurrentStore => HttpContext.GetStoreData();

    public async Task<IActionResult> Index(string storeId)
    {
        if (CurrentStore is null)
            return NotFound();

        var hasPermission = await authorizationService.AuthorizeAsync(User, CurrentStore.Id, Policies.CanModifyStoreSettingsUnscoped);
        CreateStoreViewModel vm = new CreateStoreViewModel
        {
            StoreId = CurrentStore.Id,
            HasStoreCreationPermission = hasPermission.Succeeded,
            DefaultCurrency = StoreBlobHelper.StandardDefaultCurrency,
            Exchanges = GetExchangesSelectList(null)
        };
        return View(vm);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(List<CreateStoreViewModel> model)
    {
        if (CurrentStore is null)
            return NotFound();

        string userId = GetUserId();

        foreach (var vm in model)
        {
            var store = await storeRepository.GetDefaultStoreTemplate();
            store.StoreName = vm.Name;
            var blob = store.GetStoreBlob();
            blob.DefaultCurrency = vm.DefaultCurrency;
            var rate = blob.GetOrCreateRateSettings(false);
            rate.PreferredExchange = vm.PreferredExchange;
            rate.RateScripting = false;
            store.SetStoreBlob(blob);
            await storeRepository.CreateStore(userId, store);
        }
        TempData[WellKnownTempData.SuccessMessage] = "Store(s) successfully created";
        return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
    }

    private string GetUserId() => userManager.GetUserId(User);

    private SelectList GetExchangesSelectList(string selected)
    {
        var exchanges = rateFactory.RateProviderFactory
            .AvailableRateProviders
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var chosen = exchanges.Find(f => f.Id == selected) ?? exchanges[0];
        return new SelectList(exchanges, nameof(chosen.Id), nameof(chosen.DisplayName), chosen.Id);
    }
}

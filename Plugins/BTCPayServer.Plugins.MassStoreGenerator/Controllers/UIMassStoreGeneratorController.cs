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
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.MassStoreGenerator.Data;
using BTCPayServer.Plugins.MassStoreGenerator.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services;

namespace BTCPayServer.Plugins.Template;

[Route("~/plugins/storegenerator")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIMassStoreGeneratorController : Controller
{
    private readonly RateFetcher _rateFactory;
    private readonly StoreRepository _storeRepository;
    private readonly MassStoreGeneratorDbContextFactory _contextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIMassStoreGeneratorController
        (RateFetcher rateFactory,
        StoreRepository storeRepository,
        MassStoreGeneratorDbContextFactory contextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
        _rateFactory = rateFactory;
        _contextFactory = contextFactory;
        _storeRepository = storeRepository;
    }

    public StoreData CurrentStore => HttpContext.GetStoreData();

    // GET
    public async Task<IActionResult> Index()
    {
        List<MassStoresViewModel> vm = new List<MassStoresViewModel>();
        return View(vm);
    }

    [HttpGet("~/plugins/stores/create")]
    public async Task<IActionResult> Create()
    {
        CreateStoreViewModel vm = new CreateStoreViewModel
        {
            DefaultCurrency = StoreBlob.StandardDefaultCurrency,
            Exchanges = GetExchangesSelectList(null)
        };
        return View(vm);
    }

    [HttpPost("~/plugins/stores/create")]

    public async Task<IActionResult> Create(List<CreateStoreViewModel> model)
    {
        if (CurrentStore is null)
            return NotFound();

        if (model.Any())
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Please enter store details to create",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(Index));
        }

        string userId = GetUserId();
        var stores = await _storeRepository.GetStoresByUserId(userId);

        var storeDataNames = stores.Select(sd => sd.StoreName).ToList();
        var createStoreNames = model.Select(csm => csm.Name).ToList();

        var duplicates = storeDataNames.Intersect(createStoreNames).ToList();
        if (duplicates.Any())
        {
            string duplicatesCommaSeparated = string.Join(", ", duplicates);
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Cannot complete store(s) generation. Duplicate store names: {duplicatesCommaSeparated}",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(Index));
        }
        await using var dbPlugins = _contextFactory.CreateContext();

        List<Store> entity = new List<Store>();
        foreach (var vm in model)
        {
            var store = new StoreData { StoreName = vm.Name };
            var blob = store.GetStoreBlob();
            blob.DefaultCurrency = vm.DefaultCurrency;
            blob.PreferredExchange = vm.PreferredExchange;
            store.SetStoreBlob(blob);
            await _storeRepository.CreateStore(userId, store);

            entity.Add(new Store
            {
                Id = Guid.NewGuid().ToString(),
                ApplicationUserId = userId,
                StoreName = vm.Name,
                StoreBlob = store.StoreBlob,
                StoreDataId = store.Id
            });
        }
        dbPlugins.AddRange(entity);
        await dbPlugins.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
    private string GetUserId() => _userManager.GetUserId(User);

    private SelectList GetExchangesSelectList(string selected)
    {
        var exchanges = _rateFactory.RateProviderFactory
            .AvailableRateProviders
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        exchanges.Insert(0, new(null, "Recommended", ""));
        var chosen = exchanges.FirstOrDefault(f => f.Id == selected) ?? exchanges.First();
        return new SelectList(exchanges, nameof(chosen.Id), nameof(chosen.DisplayName), chosen.Id);
    }
}

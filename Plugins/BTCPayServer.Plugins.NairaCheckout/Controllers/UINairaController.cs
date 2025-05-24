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
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;

namespace BTCPayServer.Plugins.Template;

[Route("stores/{storeId}/naira")]
[Authorize(Policy = Policies.CanModifyServerSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
public class UINairaController : Controller
{
    private readonly RateFetcher _rateFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NairaStatusProvider _nairaStatusProvider;
    public UINairaController
        (RateFetcher rateFactory,
        NairaStatusProvider nairaStatusProvider,
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
        _rateFactory = rateFactory;
        _nairaStatusProvider = nairaStatusProvider;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();


    private StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> StoreConfig()
    {
        var model = new NairaStoreViewModel { Enabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id) };

        return View(model);
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

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Services.Stores;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using BTCPayServer.Payments;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostController : Controller
{
    private readonly StoreRepository _storeRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIGhostController
        (StoreRepository storeRepo,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _userManager = userManager;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/ghost")]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        return View(new GhostSetting());
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

    private string GetUserId() => _userManager.GetUserId(User);
}

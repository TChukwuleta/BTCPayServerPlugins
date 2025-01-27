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
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin;
using Newtonsoft.Json;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostController : Controller
{
    private readonly StoreRepository _storeRepo;
    private readonly IHttpClientFactory _clientFactory;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIGhostController
        (StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        BTCPayNetworkProvider networkProvider,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/ghost")]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        if (!storeHasWallet)
        {
            return View(new GhostSetting
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasWallet = false
            });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new GhostSetting();
        return View(userStore);
    }


    [HttpPost("~/plugins/stores/{storeId}/ghost")]
    public async Task<IActionResult> Index(string storeId, GhostSetting vm, string command = "")
    {
        Console.WriteLine(JsonConvert.SerializeObject(vm));
        try
        {
            await using var ctx = _dbContextFactory.CreateContext();
            switch (command)
            {
                case "GhostSaveCredentials":
                    {
                        vm.ApiUrl = vm.ApiUrl?.TrimEnd('/');
                        var validCreds = vm?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Ghost credentials";
                            return View(vm);
                        }
                        var apiClient = new GhostAdminApiClient(_clientFactory, vm.CreateGhsotApiCredentials());
                        try
                        {
                            var validCredentials = await apiClient.ValidateGhostCredentials();
                            if (!validCredentials)
                            {
                                TempData[WellKnownTempData.ErrorMessage] = $"Invalid Ghost credentials";
                                return View(vm);
                            }
                        }
                        catch (GhostApiException err)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = $"Invalid Ghost credentials: {err.Message}";
                            return View(vm);
                        }
                        vm.IntegratedAt = DateTimeOffset.UtcNow;
                        vm.StoreId = CurrentStore.Id;
                        vm.StoreName = CurrentStore.StoreName;
                        vm.ApplicationUserId = GetUserId();
                        ctx.Update(vm);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Ghost plugin successfully updated";
                        break;
                    }
                case "GhostClearCredentials":
                    {
                        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
                        if (ghostSetting != null)
                        {
                            ctx.Remove(ghostSetting);
                            await ctx.SaveChangesAsync();
                        }
                        TempData[WellKnownTempData.SuccessMessage] = "Ghost plugin credentials cleared";
                        break;
                    }
            }
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occurred on Ghost plugin. {ex.Message}";
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
    }

    [HttpGet("~/plugins/{storeId}/ghost/create-member")]
    public async Task<IActionResult> CreateMember(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        return View(new CreateMemberViewModel());
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

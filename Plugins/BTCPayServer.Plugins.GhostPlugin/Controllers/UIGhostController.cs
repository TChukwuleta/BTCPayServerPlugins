using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
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
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Mails;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using BTCPayServer.Services.Apps;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ghost/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostController : Controller
{
    private GhostHelper helper;
    private readonly AppService _appService;
    private readonly StoreRepository _storeRepo;
    private readonly IHttpClientFactory _clientFactory;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIGhostController
        (AppService appService,
        StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        EmailSenderFactory emailSenderFactory,
        BTCPayNetworkProvider networkProvider,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _appService = appService;
        helper = new GhostHelper(_appService);
        _userManager = userManager;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _emailSenderFactory = emailSenderFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        if (!storeHasWallet)
        {
            return View(new GhostSettingViewModel
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasWallet = false
            });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new GhostSetting();
        var viewModel = helper.GhostSettingsToViewModel(ghostSetting);
        viewModel.MemberCreationUrl = Url.Action("CreateMember", "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);
        viewModel.DonationUrl = Url.Action("Donate", "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);
        viewModel.WebhookUrl = Url.Action("ReceiveWebhook", "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);
        var emailSender = await _emailSenderFactory.GetEmailSender(storeId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailSettingsConfigured && !string.IsNullOrEmpty(ghostSetting?.AdminApiKey))
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Kindly configure Email SMTP in the admin settings to be able to send reminder to subscribers",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
        }
        return View(viewModel);
    }


    [HttpPost]
    public async Task<IActionResult> Index(string storeId, GhostSettingViewModel vm, string command = "")
    {
        try
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var store = await _storeRepo.FindStore(CurrentStore.Id);
            switch (command)
            {
                case "GhostSaveCredentials":
                    {
                        var entity = helper.GhostSettingsViewModelToEntity(vm);
                        entity.ApiUrl = entity.ApiUrl?.TrimEnd('/');
                        var validCreds = entity?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Ghost credentials";
                            return View(vm);
                        }
                        var apiClient = new GhostAdminApiClient(_clientFactory, entity.CreateGhsotApiCredentials());
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
                        entity.BaseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
                        entity.IntegratedAt = DateTimeOffset.UtcNow;
                        entity.StoreId = CurrentStore.Id;
                        entity.StoreName = CurrentStore.StoreName;
                        entity.ApplicationUserId = GetUserId();
                        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStore.Id);
                        var isEmailSetup = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
                        if (isEmailSetup)
                        {
                            var settingModel = new GhostSettingsPageViewModel { ReminderStartDaysBeforeExpiration = 4, EnableAutomatedEmailReminders = true };
                            entity.Setting = JsonConvert.SerializeObject(settingModel);
                        }
                        var storeBlob = store.GetStoreBlob();
                        var newApp = await helper.CreateGhostApp(CurrentStore.Id, storeBlob.DefaultCurrency);
                        entity.AppId = newApp.Id;
                        ctx.Update(entity);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Ghost plugin successfully updated";
                        break;
                    }
                case "GhostClearCredentials":
                    {
                        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
                        if (ghostSetting != null)
                        {
                            await helper.DeleteGhostApp(CurrentStore.Id, ghostSetting.AppId);
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


    [HttpGet("settings")]
    public async Task<IActionResult> Settings(string storeId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var settingJson = ctx.GhostSettings.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id).Select(c => c.Setting).FirstOrDefault();

        var ghostSetting = settingJson != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(settingJson) : new GhostSettingsPageViewModel();

        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStore.Id);
        ViewData["StoreEmailSettingsConfigured"] = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        return View(ghostSetting);
    }


    [HttpPost("settings")]
    public async Task<IActionResult> Settings(string storeId, GhostSettingsPageViewModel model)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var entity = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        entity.Setting = JsonConvert.SerializeObject(model);
        ctx.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Ghost plugin settings successfully updated";
        return Ok();
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

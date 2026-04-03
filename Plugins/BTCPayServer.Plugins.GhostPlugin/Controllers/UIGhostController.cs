using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Emails;
using BTCPayServer.Plugins.Emails.Controllers;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.Services;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.GhostPlugin;


[Route("~/plugins/{storeId}/ghost/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
public class UIGhostController(AppService appService,
        StoreRepository storeRepo,
        EmailService emailService,
        BTCPayNetworkProvider networkProvider,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager) : Controller
{
    private GhostHelper helper = new GhostHelper(appService);
    public StoreData CurrentStore => HttpContext.GetStoreData();
    private string GetUserId() => userManager.GetUserId(User);

    [HttpGet]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        if (!storeHasWallet)
        {
            return View(new GhostSettingViewModel
            {
                CryptoCode = networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasWallet = false
            });
        }
        await using var ctx = dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new GhostSetting();
        var viewModel = helper.GhostSettingsToViewModel(ghostSetting);

        viewModel.MemberCreationUrl = Url.Action(
        nameof(UIGhostPublicController.CreateMember), "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);

        viewModel.DonationUrl = Url.Action(
        nameof(UIGhostPublicController.Donate), "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);

        viewModel.WebhookUrl = Url.Action(
        nameof(UIGhostPublicController.ReceiveWebhook), "UIGhostPublic", new { storeId = CurrentStore.Id }, Request.Scheme);

        var isEmailSettingsConfigured = await emailService.IsEmailSettingsConfigured(CurrentStore.Id);
        if (!isEmailSettingsConfigured && !string.IsNullOrEmpty(ghostSetting?.AdminApiKey))
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"Kindly <a href='{Url.Action(action: nameof(UIStoresEmailController.StoreEmailSettings), controller: "UIStoresEmail",
                    values: new
                    {
                        area = EmailsPlugin.Area,
                        storeId = CurrentStore.Id
                    })}' class='alert-link'>configure Email SMTP</a> to be able to send reminder to subscribers",
                Severity = StatusMessageModel.StatusSeverity.Info,
                AllowDismiss= true
            });
        }
        return View(viewModel);
    }


    [HttpPost]
    public async Task<IActionResult> Index(string storeId, GhostSettingViewModel vm, string command = "")
    {
        try
        {
            await using var ctx = dbContextFactory.CreateContext();
            var store = await storeRepo.FindStore(CurrentStore.Id);
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
                        entity.BaseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
                        entity.StoreId = CurrentStore.Id;
                        entity.StoreName = CurrentStore.StoreName;
                        entity.ApplicationUserId = GetUserId();
                        if (await emailService.IsEmailSettingsConfigured(CurrentStore.Id))
                        {
                            entity.Setting = JsonConvert.SerializeObject(new GhostSettingsPageViewModel
                            {
                                ReminderStartDaysBeforeExpiration = 4,
                                EnableAutomatedEmailReminders = true
                            });
                        }
                        var storeBlob = store.GetStoreBlob();
                        var newApp = await helper.CreateGhostApp(CurrentStore.Id, storeBlob.DefaultCurrency);
                        entity.AppId = newApp.Id;
                        ctx.Update(entity);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Ghost plugin successfully updated";
                        break;
                    }

                case "GhostUpdateWebhookSecret":
                    {
                        if (string.IsNullOrEmpty(vm.WebhookSecret))
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Webhook secret cannot be empty";
                            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
                        }
                        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
                        if (ghostSetting != null)
                        {
                            ghostSetting.WebhookSecret = vm.WebhookSecret;
                            ctx.Update(ghostSetting);
                            await ctx.SaveChangesAsync();
                            TempData[WellKnownTempData.SuccessMessage] = "Webhook secret updated successfully";
                        }
                        break;
                    }

                case "GhostClearCredentials":
                    {
                        var ghostSetting = ctx.GhostSettings.FirstOrDefault(c => c.StoreId == CurrentStore.Id);
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

        await using var ctx = dbContextFactory.CreateContext();
        var settingJson = ctx.GhostSettings.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id).Select(c => c.Setting).FirstOrDefault();
        var ghostSetting = settingJson != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(settingJson) : new();
        ViewData["StoreEmailSettingsConfigured"] = await emailService.IsEmailSettingsConfigured(CurrentStore.Id);   
        return View(ghostSetting);
    }


    [HttpPost("settings")]
    public async Task<IActionResult> Settings(string storeId, GhostSettingsPageViewModel model)
    {
        if (CurrentStore is null)
            return NotFound();

        if (model.EnableAutomatedEmailReminders && model.ReminderStartDaysBeforeExpiration == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Kindly specify the number of days to initiate reminder notifications";
            return RedirectToAction(nameof(Settings), new { storeId = CurrentStore.Id });
        }
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        entity.Setting = JsonConvert.SerializeObject(model);
        ctx.Update(entity);
        await ctx.SaveChangesAsync();
        TempData.SetStatusMessageModel(new StatusMessageModel()
        {
            Message = "Ghost plugin settings successfully updated",
            Severity = StatusMessageModel.StatusSeverity.Success
        });
        return RedirectToAction(nameof(Settings), new { storeId = CurrentStore.Id });
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

}

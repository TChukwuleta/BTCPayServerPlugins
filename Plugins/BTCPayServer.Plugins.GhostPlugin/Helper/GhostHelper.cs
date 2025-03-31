using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Apps;
using NBitcoin.DataEncoders;
using NBitcoin;
using System;

namespace BTCPayServer.Plugins.GhostPlugin.Helper;

public class GhostHelper
{
    private readonly AppService _appService;
    public GhostHelper(AppService appService)
    {
        _appService = appService;
    }


    public async Task<AppData> CreateGhostApp(string storeId, string defaultCurrency)
    {
        var type = _appService.GetAppType(GhostApp.AppType);
        var appData = new AppData
        {
            StoreDataId = storeId,
            Name = GhostApp.AppName,
            AppType = type != null ? type!.Type : GhostApp.AppType
        };
        await _appService.SetDefaultSettings(appData, defaultCurrency);
        await _appService.UpdateOrCreateApp(appData);
        return appData;
    }

    public async Task DeleteGhostApp(string storeId, string appId)
    {
        var type = _appService.GetAppType(GhostApp.AppType);
        var appData = new AppData
        {
            Id = appId,
            StoreDataId = storeId,
            Name = GhostApp.AppName,
            AppType = type != null ? type!.Type : GhostApp.AppType
        };
        await _appService.DeleteApp(appData);
    }

    public GhostSettingViewModel GhostSettingsToViewModel(GhostSetting ghostSetting)
    {
        return new GhostSettingViewModel
        {
            AdminApiKey = ghostSetting.AdminApiKey,
            ApiUrl = ghostSetting.ApiUrl,
            ContentApiKey = ghostSetting.ContentApiKey,
            WebhookSecret = ghostSetting.WebhookSecret,
            StoreId = ghostSetting.StoreId,
            StoreName = ghostSetting.StoreName,
            IntegratedAt = ghostSetting.IntegratedAt
        };
    }

    public GhostSetting GhostSettingsViewModelToEntity(GhostSettingViewModel vm)
    {
        return new GhostSetting
        {
            AdminApiKey = vm.AdminApiKey,
            ApiUrl = vm.ApiUrl,
            ContentApiKey = vm.ContentApiKey,
            StoreId = vm.StoreId,
            WebhookSecret = !string.IsNullOrEmpty(vm.WebhookSecret) ? vm.WebhookSecret : Encoders.Base58.EncodeData(RandomUtils.GetBytes(10)),
            StoreName = vm.StoreName,
            IntegratedAt = DateTimeOffset.UtcNow
        };
    }
}
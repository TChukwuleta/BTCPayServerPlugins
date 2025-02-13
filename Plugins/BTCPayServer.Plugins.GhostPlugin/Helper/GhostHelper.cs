using System;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Apps;

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
            Username = ghostSetting.Username,
            Password = ghostSetting.Password,
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
            Username = vm.Username,
            Password = vm.Password,
            StoreId = vm.StoreId,
            StoreName = vm.StoreName
        };
    }

    public UpdateGhostEventViewModel GhostEventToViewModel(GhostEvent vm)
    {
        return new UpdateGhostEventViewModel
        {
            Title = vm.Title,
            Description = vm.Description,
            EventLink = vm.EventLink,
            EventDate = vm.EventDate,
            Amount = vm.Amount,
            Currency = vm.Currency,
            EmailBody = vm.EmailBody,
            EmailSubject = vm.EmailSubject,
            HasMaximumCapacity = vm.HasMaximumCapacity,
            MaximumEventCapacity = vm.MaximumEventCapacity,
            StoreId = vm.StoreId,
            EventId = vm.Id
        };
    }

    public GhostEvent GhostEventViewModelToEntity(UpdateGhostEventViewModel vm)
    {
        return new GhostEvent
        {
            Title = vm.Title,
            Description = vm.Description,
            EventLink = vm.EventLink,
            EventDate = vm.EventDate,
            Amount = vm.Amount,
            Currency = vm.Currency,
            EmailBody = vm.EmailBody,
            EmailSubject = vm.EmailSubject,
            HasMaximumCapacity = vm.HasMaximumCapacity,
            MaximumEventCapacity = vm.MaximumEventCapacity,
            StoreId = vm.StoreId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
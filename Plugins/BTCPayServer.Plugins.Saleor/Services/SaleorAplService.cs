using System;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services.Stores;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorAplService
{
    private readonly StoreRepository _storeRepository;
    private const string SettingsKey = "SaleorApl";

    public SaleorAplService(StoreRepository storeRepository)
    {
        _storeRepository = storeRepository;
    }

    public async Task Set(string saleorApiUrl, string token, string storeId, string saleorDomain, string appId = "")
    {
        var entry = await _storeRepository.GetSettingAsync<AplEntry>(storeId, SettingsKey) ?? new AplEntry();
        entry.SaleorApiUrl = saleorApiUrl;
        entry.Token = token;
        entry.AppId = appId; 
        entry.StoreId = storeId;
        entry.SaleorDomain = saleorDomain;
        entry.RegisteredAt = DateTimeOffset.UtcNow;
        await _storeRepository.UpdateSetting(storeId, SettingsKey, entry);
    }

    public async Task<AplEntry> Get(string storeId)
    {
        return await _storeRepository.GetSettingAsync<AplEntry>(storeId, SettingsKey) ?? new AplEntry();
    }

    public async Task Delete(string storeId)
    {
        await _storeRepository.UpdateSetting<AplEntry>(storeId, SettingsKey, null);
    }
}
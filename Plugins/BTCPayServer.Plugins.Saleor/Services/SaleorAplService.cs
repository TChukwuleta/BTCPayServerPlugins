using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorAplService
{
    private readonly SettingsRepository _settingsRepository;
    private readonly ILogger<SaleorAplService> _logger;
    private const string SettingsKey = "SaleorApl";

    public SaleorAplService(SettingsRepository settingsRepository, ILogger<SaleorAplService> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task Set(string saleorApiUrl, string token, string storeId, string appId = "")
    {
        var store = await Load();
        store[storeId] = new AplEntry
        {
            StoreId = storeId,
            SaleorApiUrl = saleorApiUrl,
            Token = token,
            AppId = appId,
            RegisteredAt = DateTime.UtcNow
        };
        await Save(store);
        _logger.LogInformation("APL: Registered Saleor instance {Url}", saleorApiUrl);
    }

    public async Task<AplEntry?> Get(string storeId)
    {
        var store = await Load();
        return store.TryGetValue(storeId, out var entry) ? entry : null;
    }

    public async Task Delete(string storeId)
    {
        var entry = await Load();
        entry.Remove(storeId);
        await Save(entry);
    }

    public async Task<bool> Delete(string storeId, string saleorApiUrl)
    {
        var store = await Load();
        if (!store.TryGetValue(storeId, out var entry))
            return false;

        if (!string.Equals(entry.SaleorApiUrl, saleorApiUrl, StringComparison.OrdinalIgnoreCase))
            return false;

        store.Remove(storeId);
        await Save(store);
        return true;
    }

    public async Task<IEnumerable<AplEntry>> GetAll()
    {
        var entries = await Load();
        return entries.Values.ToList();
    }

    private async Task<Dictionary<string, AplEntry>> Load()
    {
        var settings = await _settingsRepository.GetSettingAsync<SaleorAplSettings>(SettingsKey);
        return settings?.Entries ?? new Dictionary<string, AplEntry>();
    }

    private async Task Save(Dictionary<string, AplEntry> entry)
    {
        await _settingsRepository.UpdateSetting(new SaleorAplSettings { Entries = entry }, SettingsKey);
    }
}

public class SaleorAplSettings
{
    public Dictionary<string, AplEntry> Entries { get; set; } = new();
}
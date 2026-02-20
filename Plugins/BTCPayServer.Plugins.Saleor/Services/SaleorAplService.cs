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

    public async Task SetAsync(string saleorApiUrl, string token, string appId = "")
    {
        var store = await LoadAsync();
        store[saleorApiUrl] = new AplEntry
        {
            SaleorApiUrl = saleorApiUrl,
            Token = token,
            AppId = appId,
            RegisteredAt = DateTime.UtcNow
        };
        await SaveAsync(store);
        _logger.LogInformation("APL: Registered Saleor instance {Url}", saleorApiUrl);
    }

    public async Task<AplEntry?> GetAsync(string saleorApiUrl)
    {
        var store = await LoadAsync();
        return store.TryGetValue(saleorApiUrl, out var entry) ? entry : null;
    }

    public async Task DeleteAsync(string saleorApiUrl)
    {
        var store = await LoadAsync();
        store.Remove(saleorApiUrl);
        await SaveAsync(store);
    }

    public async Task<IEnumerable<AplEntry>> GetAllAsync()
    {
        var store = await LoadAsync();
        return store.Values.ToList();
    }

    private async Task<Dictionary<string, AplEntry>> LoadAsync()
    {
        var settings = await _settingsRepository.GetSettingAsync<SaleorAplSettings>(SettingsKey);
        return settings?.Entries ?? new Dictionary<string, AplEntry>();
    }

    private async Task SaveAsync(Dictionary<string, AplEntry> store)
    {
        await _settingsRepository.UpdateSetting(new SaleorAplSettings { Entries = store }, SettingsKey);
    }
}
public class SaleorAplSettings
{
    public Dictionary<string, AplEntry> Entries { get; set; } = new();
}
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.PhoenixdManager.ViewModels;

namespace BTCPayServer.Plugins.PhoenixdManager.Services;

public class PhoenixdSettingsService
{
    private const string SettingsKey = "PhoenixdManager.Settings";
    private readonly ISettingsRepository _settingsRepository;

    public PhoenixdSettingsService(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<PhoenixdSettings> GetSettings()
    {
        var settings = await _settingsRepository.GetSettingAsync<PhoenixdSettings>(SettingsKey);
        return settings ?? new PhoenixdSettings();
    }

    public async Task SetSettings(PhoenixdSettings settings)
    {
        await _settingsRepository.UpdateSetting(settings, SettingsKey);
    }

    public async Task<bool> IsConfigured()
    {
        var s = await GetSettings();
        return !string.IsNullOrWhiteSpace(s.ServerUrl) && !string.IsNullOrWhiteSpace(s.Password);
    }
}

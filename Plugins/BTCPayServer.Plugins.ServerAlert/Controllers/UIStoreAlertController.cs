using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.ServerAlert.Data;
using BTCPayServer.Plugins.ServerAlert.ViewModels;
using BTCPayServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.ServerAlert;


[Route("~/plugins/{storeId}/store/alerts/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[AutoValidateAntiforgeryToken]
public class UIStoreAlertController(SettingsRepository settingsRepository) : Controller
{
    private StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("monitor-store")]
    public async Task<IActionResult> StoreMonitorSettings(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        var key = $"StoreMonitor_{CurrentStore.Id}";
        var settings = await settingsRepository.GetSettingAsync<StoreMonitorSettings>(key) ?? new();
        return View(StoreMonitorViewModel.FromSettings(settings, storeId, CurrentStore.StoreName));
    }

    [HttpPost("monitor-store")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StoreMonitorSettings(string storeId, StoreMonitorViewModel model)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.StoreId = storeId;
            model.StoreName = CurrentStore.StoreName;
            return View(model);
        }

        var key = $"StoreMonitor_{CurrentStore.Id}";
        await settingsRepository.UpdateSetting(model.ToSettings(), key);
        TempData[WellKnownTempData.SuccessMessage] = "Store health monitor settings saved.";
        return RedirectToAction(nameof(StoreMonitorSettings), new { storeId });
    }
}

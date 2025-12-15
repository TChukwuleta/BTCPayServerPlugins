using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/satoshi-ticket/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISatoshiTicketController : Controller
{
    private readonly StoreRepository _storeRepository;
    public UISatoshiTicketController(StoreRepository storeRepository)
    {
        _storeRepository = storeRepository;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();


    [HttpGet("settings")]
    public async Task<IActionResult> SatoshiTicketSettings(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        var settings = await _storeRepository.GetSettingAsync<SatoshiTicketSettings>(storeId, Plugin.SettingsName) ?? new SatoshiTicketSettings();
        return View(settings);
    }


    [HttpPost("settings")]
    public async Task<IActionResult> SatoshiTicketSettings(string storeId, SatoshiTicketSettings model)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        if (model.EventReminderDaysBefore is < 0 or > 10)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please enter a reminder day between 1 and 10 days before the event";
            return RedirectToAction(nameof(SatoshiTicketSettings), new { storeId = CurrentStore.Id });
        }

        var settings = await _storeRepository.GetSettingAsync<SatoshiTicketSettings>(CurrentStore.Id, Plugin.SettingsName) ?? new SatoshiTicketSettings();
        settings.EventReminderDaysBefore = model.EventReminderDaysBefore;
        settings.EnableEventAutoReminder = model.EnableEventAutoReminder;
        await _storeRepository.UpdateSetting(CurrentStore.Id, Plugin.SettingsName, settings);

        TempData[WellKnownTempData.SuccessMessage] = "Satoshi ticket settings saved successfully";
        return RedirectToAction(nameof(SatoshiTicketSettings), new { storeId = CurrentStore.Id });
    }
}

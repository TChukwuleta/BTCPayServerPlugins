using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.JumpSeller.Data;
using BTCPayServer.Plugins.JumpSeller.Services;
using BTCPayServer.Plugins.JumpSeller.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.JumpSeller;

[Route("~/plugins/{storeId}/jumpseller/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
public class UIJumpSellerController : Controller
{
    private readonly JumpSellerService _jumpSellerService;

    public UIJumpSellerController(JumpSellerService jumpSellerService)
    {
        _jumpSellerService = jumpSellerService;
    }
    private BTCPayServer.Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> Settings(string storeId)
    {
        var settings = await _jumpSellerService.GetSettings(storeId) ?? new JumpSellerStoreSetting();
        var vm = new JumpSellerSettingsViewModel
        {
            EpgAccountId = settings.EpgAccountId,
            EpgSecret = settings.EpgSecret,
            StoreId = storeId,
            PaymentUrl = PaymentUrl(storeId)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Settings(string storeId, JumpSellerSettingsViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.StoreId = storeId;
            vm.PaymentUrl = PaymentUrl(storeId);
            return View(vm);
        }
        await _jumpSellerService.SaveSettings(storeId, vm);
        TempData[WellKnownTempData.SuccessMessage] = "JumpSeller settings saved successfully";
        return RedirectToAction(nameof(Settings), new { storeId });
    }

    private string PaymentUrl(string storeId) => Url.Action(nameof(UIJumpSellerPaymentController.Pay), "UIJumpSellerPayment", new { storeId }, Request.Scheme);
}

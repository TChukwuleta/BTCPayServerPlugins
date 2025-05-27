using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;

namespace BTCPayServer.Plugins.Template;

[AllowAnonymous]
[Route("~/plugins/{storeId}/naira-checkout/public/", Order = 0)]
[Route("~/plugins/{storeId}/naira-checkout/api/", Order = 1)]
public class UINairaPublicController : Controller
{
    private readonly NairaStatusProvider _nairaStatusProvider;
    public UINairaPublicController(NairaStatusProvider nairaStatusProvider)
    {
        _nairaStatusProvider = nairaStatusProvider;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();


    private StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet("webhook")]
    public async Task<IActionResult> ReceiveWebhook()
    {
        var model = new NairaStoreViewModel { Enabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id) };
        return Ok(model);
    }
}

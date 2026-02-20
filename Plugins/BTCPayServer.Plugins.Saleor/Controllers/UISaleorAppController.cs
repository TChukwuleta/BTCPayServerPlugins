using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Saleor.Services;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Saleor;

[Route("~/plugins/{storeId}/saleor/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISaleorAppController : Controller
{
    private readonly SaleorAplService _apl;
    private readonly StoreRepository _storeRepository;
    public UISaleorAppController(SaleorAplService apl,StoreRepository storeRepository) 
    {

        _apl = apl;
        _storeRepository = storeRepository;
    }

    public Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> Index(string storeId)
    {
        if (CurrentStore == null) 
            return NotFound();

        var entity = await _apl.Get(CurrentStore.Id);

        return View(new SaleorDashboardViewModel
        {
            StoreId = storeId,
            StoreName = CurrentStore.StoreName,
            ManifestUrl = Url.Action(nameof(UISaleorPublicAppController.GetManifest), "UISaleorPublicApp", new { storeId = CurrentStore.Id }, Request.Scheme),
            ConnectedInstance = entity
        });
    }
}

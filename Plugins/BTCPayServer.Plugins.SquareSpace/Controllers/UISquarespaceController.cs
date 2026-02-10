using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.StoreBridge;

[Route("~/plugins/{storeId}/squarespace/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISquarespaceController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly StoreImportExportService _service;
    private readonly UserManager<ApplicationUser> _userManager;
    public UISquarespaceController(StoreImportExportService service,UserManager<ApplicationUser> userManager,
        StoreRepository storeRepository)
    {
        _service = service;
        _userManager = userManager;
        _storeRepository = storeRepository;
    }
    private StoreData CurrentStore => HttpContext.GetStoreData();
    private string GetBaseUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
    private string GetUserId() => _userManager.GetUserId(User);



    /*[HttpGet("templates")]
    public async Task<IActionResult> TemplateGallery(string storeId)
    {
        if (CurrentStore == null) return NotFound();

        var templates = await _templateService.GetAllTemplates();
        var vm = new TemplateGalleryViewModel
        {
            Templates = templates.Select(t => new TemplateViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Tags = t.Tags?.Split(',').Select(tag => tag.Trim()).ToList() ?? new(),
                UploadedBy = t.UploadedBy,
                UploadedAt = t.UploadedAt,
                DownloadCount = t.DownloadCount,
                IncludedOptions = JsonConvert.DeserializeObject<List<string>>(t.IncludedOptions ?? "[]")
            }).ToList()
        };
        return View(vm);
    }


    [HttpPost("templates/{id}/use")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UseTemplate(string storeId, string id)
    {
        if (CurrentStore == null) return NotFound();

        var template = await _templateService.GetTemplate(id);
        if (template == null) return NotFound();

        await _templateService.IncrementDownloadCount(id);

        var getPreview = GetImportPreview(template.FileData);
        if (!string.IsNullOrEmpty(getPreview.errorMessage))
        {
            TempData[WellKnownTempData.ErrorMessage] = getPreview.errorMessage;
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
        TempData["ImportFileData"] = Convert.ToBase64String(template.FileData);
        TempData[WellKnownTempData.SuccessMessage] = "Export file validated successfully. Review and select what to import";
        return View(nameof(ImportStore), getPreview.model);
    }*/

}

using System;
using System.IO;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.Services;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.StoreBridge;

[Route("~/plugins/{storeId}/storebridge/admin/")]
[Authorize(Policy = Policies.CanModifyServerSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
public class UIStoreBridgeAdminController : Controller
{
    private readonly TemplateService _templateService;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIStoreBridgeAdminController(UserManager<ApplicationUser> userManager, TemplateService templateService)
    {
        _userManager = userManager;
        _templateService = templateService;
    }
    private StoreData CurrentStore => HttpContext.GetStoreData();
    private string GetUserId() => _userManager.GetUserId(User);


    [HttpGet("templates")]
    public async Task<IActionResult> ManageTemplates(string storeId)
    {
        if (CurrentStore == null) return NotFound();

        var templates = await _templateService.GetAllTemplates();
        return View(new ManageTemplatesViewModel
        {
            Templates = templates
        });
    }

    [HttpGet("templates/upload")]
    public IActionResult UploadTemplate(string storeId)
    {
        if (CurrentStore == null) return NotFound();

        return View(new UploadTemplateViewModel());
    }

    [HttpPost("templates/upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTemplate(string storeId, UploadTemplateViewModel vm)
    {
        if (CurrentStore == null) return NotFound();

        if (!ModelState.IsValid) return View(vm);

        if (vm.TemplateFile == null || vm.TemplateFile.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.TemplateFile), "Please select a template file");
            return View(vm);
        }
        if (!vm.TemplateFile.FileName.EndsWith(".storebridge", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(vm.TemplateFile), "Invalid file format. Must be .storebridge");
            return View(vm);
        }

        byte[] fileBytes;
        using (var ms = new MemoryStream())
        {
            await vm.TemplateFile.CopyToAsync(ms);
            fileBytes = ms.ToArray();
        }
        var template = await _templateService.UploadTemplate(new TemplateDataViewModel
        {
            FileData = fileBytes,
            Description = vm.Description,
            Name = vm.Name,
            Tags = vm.Tags,
            UploadedBy = GetUserId()
        });
        if (!template.success)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured while uploading template. {template.message}";
            return RedirectToAction(nameof(UploadTemplate), new { storeId });
        }
        TempData[WellKnownTempData.SuccessMessage] = "Template uploaded successfully";
        return RedirectToAction(nameof(ManageTemplates), new { storeId });
    }

    [HttpPost("templates/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(string storeId, string id)
    {
        if (CurrentStore == null) return NotFound();

        await _templateService.DeleteTemplate(id);
        TempData[WellKnownTempData.SuccessMessage] = "Template deleted successfully";
        return RedirectToAction(nameof(ManageTemplates), new { storeId });
    }
}

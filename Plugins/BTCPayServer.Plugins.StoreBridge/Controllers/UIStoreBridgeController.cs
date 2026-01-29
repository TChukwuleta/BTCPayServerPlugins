using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.Services;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.StoreBridge;

[Route("~/plugins/{storeId}/storebridge/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIStoreBridgeController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly StoreImportExportService _service;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIStoreBridgeController(StoreImportExportService service,UserManager<ApplicationUser> userManager,
        StoreRepository storeRepository)
    {
        _service = service;
        _userManager = userManager;
        _storeRepository = storeRepository;
    }
    private StoreData CurrentStore => HttpContext.GetStoreData();
    private string GetBaseUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
    private string GetUserId() => _userManager.GetUserId(User);


    [HttpGet("export")]
    public IActionResult ExportStore(string storeId)
    {
        if (CurrentStore == null) return NotFound();
        return View(new ExportViewModel
        {
            StoreId = CurrentStore.Id,
            SelectedOptions = ExportViewModel.AllOptions
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportStorePost(string storeId, ExportViewModel vm)
    {
        if (CurrentStore == null) return NotFound();
        try
        {
            var store = await _storeRepository.FindStore(CurrentStore.Id);
            var encryptedData = await _service.ExportStore(GetBaseUrl(), GetUserId(), store, vm.SelectedOptions);
            var filename = $"btcpay-store-{DateTime.UtcNow.Ticks}.storebridge";
            return File(encryptedData, "application/octet-stream", filename);
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Export failed: {ex.Message}";
            return RedirectToAction(nameof(ExportStore), new { storeId });
        }
    }

    [HttpGet("import")]
    public IActionResult ImportStore(string storeId)
    {
        if (CurrentStore == null) return NotFound();

        return View(new ImportViewModel
        {
            StoreId = CurrentStore.Id,
            ShowPreview = false
        });
    }

    [HttpPost("import/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStorePreview(ImportViewModel vm)
    {
        if (CurrentStore == null) return NotFound();
        if (vm.ImportFile == null || vm.ImportFile.Length == 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please select a file to import";
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
        if (!vm.ImportFile.FileName.EndsWith(".storebridge", StringComparison.OrdinalIgnoreCase))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid file format. Please upload a .storebridge file";
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
        if (vm.ImportFile.Length > 1 * 1024 * 1024)
        {
            TempData[WellKnownTempData.ErrorMessage] = "File size exceeds 1MB limit";
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
        try
        {
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await vm.ImportFile.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }
            var getPreview = GetImportPreview(fileBytes);
            if (!string.IsNullOrEmpty(getPreview.errorMessage))
            {
                TempData[WellKnownTempData.ErrorMessage] = getPreview.errorMessage;
                return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
            }
            TempData["ImportFileData"] = Convert.ToBase64String(fileBytes);
            TempData[WellKnownTempData.SuccessMessage] = "Export file validated successfully. Review and select what to import";
            return View(nameof(ImportStore), getPreview.model);
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Failed to process export file: {ex.Message}";
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
    }

    [HttpPost("import/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStoreConfirm(ImportViewModel vm)
    {
        if (CurrentStore == null) return NotFound();

        var store = await _storeRepository.FindStore(CurrentStore.Id);
        try
        {
            var fileDataBase64 = TempData["ImportFileData"] as string;
            if (string.IsNullOrEmpty(fileDataBase64))
            {
                TempData[WellKnownTempData.ErrorMessage] = "Import session expired. Please upload the file again.";
                return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
            }

            var fileBytes = Convert.FromBase64String(fileDataBase64);

            if (vm.SelectedOptions == null || !vm.SelectedOptions.Any())
            {
                var getPreview = GetImportPreview(fileBytes, vm);
                if (!string.IsNullOrEmpty(getPreview.errorMessage))
                {
                    TempData[WellKnownTempData.ErrorMessage] = getPreview.errorMessage;
                    return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
                }
                TempData[WellKnownTempData.ErrorMessage] = "Please select at least one item to import";
                TempData["ImportFileData"] = fileDataBase64;
                return View(nameof(ImportStore), getPreview.model);
            }
            var (success, message) = await _service.ImportStore(store, fileBytes, GetUserId(), vm.SelectedOptions);
            if (success)
            {
                TempData[WellKnownTempData.SuccessMessage] = message;
                return RedirectToAction("Dashboard", "UIStores", new { storeId = CurrentStore.Id });
            }
            else
            {
                TempData[WellKnownTempData.ErrorMessage] = message;
                return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
            }
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Import failed: {ex.Message}";
            return RedirectToAction(nameof(ImportStore), new { storeId = CurrentStore.Id });
        }
    }

    private (string errorMessage, ImportViewModel model) GetImportPreview(byte[] fileBytes, ImportViewModel vm = null)
    {
        StoreExportData exportData = _service.GetExportPreview(fileBytes);
        if (exportData == null || exportData?.Store == null)
        {
            return ("Failed to decrypt export file. This file may be corrupted or encrypted for a different store", null);
        }
        if (exportData.Store.StoreId == CurrentStore.Id)
        {
            return ("Cannot import store configuration into the same store", null);
        }
        var availableOptions = _service.GetAvailableImportOptions(fileBytes, CurrentStore.Id);
        if (!availableOptions.Any())
        {
            return ("The export file contains no importable data", null);
        }
        ImportViewModel previewModel;
        if (vm == null)
        {
            previewModel = new ImportViewModel
            {
                StoreId = CurrentStore.Id,
                ShowPreview = true,
                ExportedFrom = exportData.ExportedFrom,
                ExportDate = exportData.ExportDate,
                OriginalStoreName = exportData.Store?.StoreName,
                AvailableOptions = availableOptions,
                SelectedOptions = availableOptions
            };
        }
        else
        {
            previewModel = vm;
            previewModel.ShowPreview = true;
            previewModel.ExportedFrom = exportData.ExportedFrom;
            previewModel.ExportDate = exportData.ExportDate;
            previewModel.OriginalStoreName = exportData.Store?.StoreName;
            previewModel.AvailableOptions = availableOptions;
        }
        return (string.Empty, previewModel);
    }
}

using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.StoreBridge;

[Route("~/plugins/{storeId}/storebridge/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIStoreBridgeController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly StoreImportExportService _service;
    private readonly ILogger<UIStoreBridgeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIStoreBridgeController(StoreImportExportService service,UserManager<ApplicationUser> userManager,
        StoreRepository storeRepository, ILogger<UIStoreBridgeController> logger)
    {
        _logger = logger;
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
            SelectedOptions = new List<string>(ExportViewModel.AllOptions)
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportStorePost(string storeId, ExportViewModel vm)
    {
        try
        {
            if (CurrentStore == null) return NotFound();

            var store = await _storeRepository.FindStore(CurrentStore.Id);
            var encryptedData = await _service.ExportStore(GetBaseUrl(), GetUserId(), store, vm.SelectedOptions);
            var filename = $"btcpay-store-{store.StoreName}-{DateTime.UtcNow:yyyyMMddHHmmss}.storebridge";
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
            return View(nameof(ImportStore), vm);
        }
        if (!vm.ImportFile.FileName.EndsWith(".storebridge", StringComparison.OrdinalIgnoreCase))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid file format. Please upload a .storebridge file";
            return View(nameof(ImportStore), vm);
        }
        if (vm.ImportFile.Length > 1 * 1024 * 1024)
        {
            TempData[WellKnownTempData.ErrorMessage] = "File size exceeds 1MB limit";
            return View(nameof(ImportStore), vm);
        }

        try
        {
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await vm.ImportFile.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            StoreExportData exportData = _service.GetExportPreview(fileBytes, CurrentStore.Id);
            if (exportData == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Failed to decrypt export file. This file may be corrupted or encrypted for a different store";
                return View(nameof(ImportStore), vm);
            }

            // Validate version
            if (exportData.Version != 1)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Unsupported export version: {exportData.Version}. Please use a compatible export file.";
                return View(nameof(ImportStore), vm);
            }

            var availableOptions = _service.GetAvailableImportOptions(fileBytes, CurrentStore.Id);
            if (!availableOptions.Any())
            {
                TempData[WellKnownTempData.ErrorMessage] = "The export file contains no importable data";
                return View(nameof(ImportStore), vm);
            }

            var previewModel = new ImportViewModel
            {
                StoreId = CurrentStore.Id,
                ShowPreview = true,
                ExportedFrom = exportData.ExportedFrom,
                ExportDate = exportData.ExportDate,
                OriginalStoreName = exportData.Store?.StoreName,
                AvailableOptions = availableOptions,
                SelectedOptions = new List<string>(availableOptions) 
            };

            TempData["ImportFileData"] = Convert.ToBase64String(fileBytes);
            TempData[WellKnownTempData.SuccessMessage] = "Export file validated successfully. Review and select what to import.";

            return View(nameof(ImportStore), vm);
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Failed to process export file: {ex.Message}";
            return View(nameof(ImportStore), vm);
        }
    }

    [HttpPost("import/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStoreConfirm(ImportViewModel vm)
    {
        if (CurrentStore == null) return NotFound();

        var store = await _storeRepository.FindStore(CurrentStore.Id);
        // Retrieve the stored file data
        var fileDataBase64 = TempData["ImportFileData"] as string;
        if (string.IsNullOrEmpty(fileDataBase64))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Import session expired. Please upload the file again.";
            return RedirectToAction(nameof(ImportStore));
        }

        // Validate that at least one option is selected
        if (vm.SelectedOptions == null || !vm.SelectedOptions.Any())
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please select at least one item to import";

            // Restore TempData for retry
            TempData["ImportFileData"] = fileDataBase64;

            // Recreate the preview
            try
            {
                var fileBytes = Convert.FromBase64String(fileDataBase64);
                StoreExportData exportData = _service.GetExportPreview(fileBytes, CurrentStore.Id);
                if (exportData == null)
                {
                    TempData[WellKnownTempData.ErrorMessage] = $"Failed to decrypt export file. This file may be corrupted or encrypted for a different store";
                    return RedirectToAction(nameof(ImportStore));
                }
                var availableOptions = _service.GetAvailableImportOptions(fileBytes, CurrentStore.Id);
                if (!availableOptions.Any())
                {
                    TempData[WellKnownTempData.ErrorMessage] = "The export file contains no importable data";
                    return View(nameof(ImportStore), vm);
                }

                vm.ShowPreview = true;
                vm.ExportedFrom = exportData.ExportedFrom;
                vm.ExportDate = exportData.ExportDate;
                vm.OriginalStoreName = exportData.Store?.StoreName;
                vm.AvailableOptions = availableOptions;

                return View("ImportStore", vm);
            }
            catch
            {
                return RedirectToAction(nameof(ImportStore));
            }
        }

        try
        {
            var fileBytes = Convert.FromBase64String(fileDataBase64);

            // Perform the import
            var (success, message) = await _service.ImportStore(store, fileBytes, GetUserId(), vm.SelectedOptions);

            if (success)
            {
                TempData[WellKnownTempData.SuccessMessage] = message;
                return RedirectToAction("Dashboard", "UIStores", new { storeId = CurrentStore.Id });
            }
            else
            {
                TempData[WellKnownTempData.ErrorMessage] = message;
                return RedirectToAction(nameof(ImportStore));
            }
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Import failed: {ex.Message}";
            return RedirectToAction(nameof(ImportStore));
        }
    }
}

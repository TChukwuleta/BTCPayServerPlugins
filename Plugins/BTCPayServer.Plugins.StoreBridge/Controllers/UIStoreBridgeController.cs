using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var filename = $"btcpay-store-{store.StoreName}-{DateTime.UtcNow:yyyyMMddHHmmss}.btcpayexport";
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
        return View(new ImportViewModel
        {
            StoreId = CurrentStore.Id,
            Options = new StoreImportOptions()
        });
    }


    [HttpPost("import")]
    public async Task<IActionResult> ImportStorePost(IFormFile importFile, StoreImportOptions options)
    {
        if (importFile == null || importFile.Length == 0)
        {
            ModelState.AddModelError(nameof(importFile), "Please select a file to import");
            return View(nameof(ImportStore), new ImportViewModel { Options = options });
        }

        if (!importFile.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(importFile), "Only JSON files are supported");
            return View(nameof(ImportStore), new ImportViewModel { Options = options });
        }

        try
        {
            string json;
            using (var reader = new StreamReader(importFile.OpenReadStream()))
            {
                json = await reader.ReadToEndAsync();
            }

            var exportData = _service.DeserializeImport(json);
            var currentUserId = HttpContext.User.Claims
                .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(currentUserId))
                throw new InvalidOperationException("User not authenticated");

            var result = await _service.ImportStoreAsync(exportData, options, currentUserId);

            if (result.Success)
            {
                var message = new StringBuilder();
                message.AppendLine($"Store imported successfully! New Store ID: {result.NewStoreId}");
                message.AppendLine($"Wallets: {result.Statistics.WalletsImported}");
                message.AppendLine($"Payment Methods: {result.Statistics.PaymentMethodsImported}");
                message.AppendLine($"Webhooks: {result.Statistics.WebhooksImported}");
                message.AppendLine($"Users: {result.Statistics.UsersImported}");
                message.AppendLine($"Apps: {result.Statistics.AppsImported}");

                if (result.Warnings.Any())
                {
                    message.AppendLine("\nWarnings:");
                    foreach (var warning in result.Warnings)
                    {
                        message.AppendLine($"- {warning}");
                    }
                }

                TempData[WellKnownTempData.SuccessMessage] = message.ToString();
                return RedirectToAction("Dashboard", "UIStores", new { storeId = result.NewStoreId });
            }
            else
            {
                var errorMessage = "Import failed:\n" + string.Join("\n", result.Errors);
                TempData[WellKnownTempData.ErrorMessage] = errorMessage;
                return View(nameof(ImportStore), new ImportViewModel { Options = options });
            }
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Import failed: {ex.Message}";
            return View(nameof(ImportStore), new ImportViewModel { Options = options });
        }
    }
}

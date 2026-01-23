using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.StoreBridge;

[Route("~/plugins/storesgenerator")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIStoreBridgeController : Controller
{
    private readonly RateFetcher _rateFactory;
    private readonly StoreRepository _storeRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIStoreBridgeController
        (RateFetcher rateFactory, StoreRepository storeRepository,
        UserManager<ApplicationUser> userManager,IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _rateFactory = rateFactory;
        _storeRepository = storeRepository;
        _authorizationService = authorizationService;
    }
    public Data.StoreData CurrentStore => HttpContext.GetStoreData();
    private string GetUserId() => _userManager.GetUserId(User);


    [HttpGet("export")]
    public IActionResult Export()
    {
        var storeId = HttpContext.GetStoreData()?.Id;
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        return View(new ExportViewModel { StoreId = storeId });
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportStore(string storeId)
    {
        try
        {
            var exportData = await _service.ExportStoreAsync(storeId);
            var json = _service.SerializeExport(exportData);
            var bytes = Encoding.UTF8.GetBytes(json);

            var fileName = $"store-export-{exportData.Store.StoreName.Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

            return File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Export failed: {ex.Message}";
            return RedirectToAction(nameof(Export));
        }
    }

    [HttpGet("import")]
    public IActionResult Import()
    {
        var storeId = HttpContext.GetStoreData()?.Id;
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        return View(new ImportViewModel
        {
            Options = new StoreImportOptions()
        });
    }


    [HttpPost("import")]
    public async Task<IActionResult> ImportStore(IFormFile importFile, StoreImportOptions options)
    {
        if (importFile == null || importFile.Length == 0)
        {
            ModelState.AddModelError(nameof(importFile), "Please select a file to import");
            return View(nameof(Import), new ImportViewModel { Options = options });
        }

        if (!importFile.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(importFile), "Only JSON files are supported");
            return View(nameof(Import), new ImportViewModel { Options = options });
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
                return View(nameof(Import), new ImportViewModel { Options = options });
            }
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Import failed: {ex.Message}";
            return View(nameof(Import), new ImportViewModel { Options = options });
        }
    }
}

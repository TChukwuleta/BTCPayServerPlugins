using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Filters;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Plugins.LightSpeed.Data;
using BTCPayServer.Plugins.LightSpeed.Services;
using BTCPayServer.Plugins.LightSpeed.ViewModels;
using BTCPayServer.Services.Stores;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.LightSpeed;

[Route("~/plugins/{storeId}/lightspeedhq/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
public class UILightSpeedController : Controller
{
    private readonly ILogger<UILightSpeedController> _logger;
    private readonly StoreRepository _storeRepository;
    private readonly LightSpeedService _lightSpeedService;
    private readonly UIInvoiceController _invoiceController;

    public UILightSpeedController(StoreRepository storeRepository, LightSpeedService lightSpeedService, UIInvoiceController invoiceController,
        ILogger<UILightSpeedController> logger)
    {
        _logger = logger;
        _storeRepository = storeRepository;
        _lightSpeedService = lightSpeedService;
        _invoiceController = invoiceController;
    }
    private BTCPayServer.Data.StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(string storeId)
    {
        var settings = await _lightSpeedService.GetSettings(CurrentStore.Id) ?? new LightspeedSettings { StoreId = storeId };

        return View(new LightspeedSettingsViewModel
        {
            StoreId = settings.StoreId,
            LightSpeedUrl = settings.LightSpeedUrl,
            LightspeedPersonalAccessToken = settings.LightspeedPersonalAccessToken,
            Currency = settings.Currency,
            GatewayUrl = Url.Action(nameof(Gateway), "UILightSpeed", new { storeId }, Request.Scheme)
        });
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(string storeId, LightspeedSettingsViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.StoreId = storeId;
        _logger.LogInformation(JsonConvert.SerializeObject(model, Formatting.Indented));
        await _lightSpeedService.SaveSettings(model);
        TempData[WellKnownTempData.SuccessMessage] = "Lightspeed HQ settings saved";
        return RedirectToAction(nameof(Settings), new { storeId });
    }

    static AsyncDuplicateLock OrderLocks = new AsyncDuplicateLock();

    [HttpGet("gateway")]
    [AllowAnonymous]
    [XFrameOptions(XFrameOptionsAttribute.XFrameOptions.Unset)]
    public async Task<IActionResult> Gateway(
        string storeId,
        [FromQuery] decimal amount, [FromQuery] string register_id, [FromQuery] string origin,
        [FromQuery] string? currency, [FromQuery] string? retailer_payment_type_id, [FromQuery] string? customer_id, [FromQuery] string? reference_id)
    {
        var settings = await _lightSpeedService.GetSettings(storeId);
        var store = await _storeRepository.FindStore(storeId);
        if (store == null || settings is null || !settings.IsConfigured)
            return BadRequest("Plugin not configured for this store");

        if (string.IsNullOrEmpty(currency))
            return BadRequest("Currency not present");

        var expectedOrigin = settings.LightSpeedUrl.TrimEnd('/');
        if (!origin.TrimEnd('/').Equals(expectedOrigin, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid origin");

        using var l = await OrderLocks.LockAsync(register_id, CancellationToken.None);

        var invoice = await _invoiceController.CreateInvoiceCoreRaw(new CreateInvoiceRequest()
        {
            Amount = amount,
            Currency = currency,
            Metadata = new JObject
            {
                ["orderId"] = reference_id ?? register_id,
                ["lightspeedRegisterId"] = register_id,
                ["lightspeedOrigin"] = origin,
                ["lightspeedCustomerId"] = customer_id,
                ["lightspeedReferenceId"] = reference_id,
                ["lightspeedPaymentTypeId"] = retailer_payment_type_id,
            },
            AdditionalSearchTerms = [register_id]
        }, store, Request.GetAbsoluteRoot(), [register_id], CancellationToken.None);

        await _lightSpeedService.AddLightSpeedPayment(new LightSpeedPayment
        {
            InvoiceId = invoice.Id,
            StoreId = storeId,
            RegisterSaleId = register_id,
            Amount = amount,
            Currency = currency
        });
        ViewBag.InvoiceId = invoice.Id;
        ViewBag.InvoiceUrl = CheckoutUrl(invoice.Id);
        ViewBag.EventsUrl = Url.Action(nameof(Status), "UILightspeed", new { storeId, invoiceId = invoice.Id }, Request.Scheme);
        ViewBag.Origin = origin;
        ViewBag.Amount = amount;
        ViewBag.Currency = currency;
        return View();
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status(string storeId, string invoiceId)
    {
        var payment = await _lightSpeedService.GetPayment(invoiceId);
        var store = await _storeRepository.FindStore(storeId);
        if (store is null || payment is null)
            return NotFound();

        return Ok(new
        {
            invoiceId,
            status = payment.Status.ToString().ToLowerInvariant(),
            settled = payment.Status == LightSpeedPaymentStatus.Settled,
            failed = payment.Status is LightSpeedPaymentStatus.Expired or LightSpeedPaymentStatus.Failed
        });
    }

    private string CheckoutUrl(string invoiceId) => Url.Action(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId }, Request.Scheme);
}

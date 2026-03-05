using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Plugins.JumpSeller.Data;
using BTCPayServer.Plugins.JumpSeller.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.JumpSeller;

[AllowAnonymous]
[Route("~/plugins/{storeId}/jumpseller/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
public class UIJumpSellerPaymentController : Controller
{
    private readonly ILogger<UIJumpSellerPaymentController> _logger;
    private readonly StoreRepository _storeRepository;
    private readonly JumpSellerService _jumpSellerService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;

    public UIJumpSellerPaymentController(StoreRepository storeRepository, JumpSellerService jumpSellerService, UIInvoiceController invoiceController,
        ILogger<UIJumpSellerPaymentController> logger, InvoiceRepository invoiceRepository)
    {
        _logger = logger;
        _storeRepository = storeRepository;
        _jumpSellerService = jumpSellerService;
        _invoiceController = invoiceController;
        _invoiceRepository = invoiceRepository;
    }

    static AsyncDuplicateLock OrderLocks = new AsyncDuplicateLock();

    [HttpPost("pay")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Pay([FromRoute] string storeId)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (string.IsNullOrEmpty(rawBody))
        {
            await Request.ReadFormAsync();
            rawBody = string.Join("&", Request.Form
                .SelectMany(kv => kv.Value.Select(v => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(v ?? ""))));
        }

        var fields = rawBody
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0].Replace("+", " ")),
                parts => Uri.UnescapeDataString(parts[1].Replace("+", " "))
            );

        string F(string key) => fields.TryGetValue(key, out var v) ? v : "";

        var settings = await _jumpSellerService.GetSettings(storeId);
        var store = await _storeRepository.FindStore(storeId);
        if (settings is null || string.IsNullOrEmpty(settings.EpgAccountId) || store is null)
        {
            return BadRequest("JumpSeller integration is not configured for this store");
        }
        if (!_jumpSellerService.VerifySignature(fields, settings.EpgSecret))
        {
            return BadRequest("Signature verification failed.");
        }
        string reference = F("x_reference");
        var amount = decimal.Parse(F("x_amount"), System.Globalization.CultureInfo.InvariantCulture);
        var itemDesc = F("x_description").Replace("\\n", "\n").Split('\n')
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("Product:")) ?.Trim() ?? F("x_description"); 

        using var l = await OrderLocks.LockAsync(reference, CancellationToken.None);

        var invoice = await _invoiceController.CreateInvoiceCoreRaw(new CreateInvoiceRequest
        {
            Amount = amount,
            Currency = F("x_currency"),
            Checkout = new InvoiceDataBase.CheckoutOptions
            {
                RedirectURL = Url.Action(nameof(RedirectPayment), "UIJumpSellerPayment",
                    new { reference = F("x_reference"), storeId }, Request.Scheme),
            },
            Metadata = new InvoiceMetadata
            {
                OrderId = reference,
                BuyerEmail = F("x_customer_email"),
                BuyerName = $"{F("x_customer_first_name")} {F("x_customer_last_name")}".Trim(),
                ItemDesc = itemDesc,
            }.ToJObject(),
        }, store, Request.GetAbsoluteRoot(), [reference], CancellationToken.None);

        await _jumpSellerService.SaveInvoiceData(new JumpSellerInvoice
        {
            InvoiceId = invoice.Id,
            StoreId = storeId,
            OrderReference = F("x_reference"),
            CallbackUrl = F("x_url_callback"),
            CompleteUrl = F("x_url_complete"),
            CancelUrl = F("x_url_cancel"),
            Amount = F("x_amount"),
            Currency = F("x_currency"),
        });
        return Redirect(CheckoutUrl(invoice.Id));
    }


    [HttpGet("redirect")]
    public async Task<IActionResult> RedirectPayment([FromRoute] string storeId, [FromQuery] string reference)
    {
        if (string.IsNullOrEmpty(reference))
            return BadRequest("Missing reference");

        var invoiceData = await _jumpSellerService.GetInvoiceDataByReference(storeId, reference);
        if (invoiceData is null)
            return NotFound("Invoice mapping not found.");

        var invoice = await _invoiceRepository.GetInvoice(invoiceData.InvoiceId);
        if (invoice is null)
            return NotFound("BTCPay invoice not found.");

        var settings = await _jumpSellerService.GetSettings(invoiceData.StoreId);
        if (settings is null)
            return BadRequest("Store not configured.");

        var result = _jumpSellerService.MapInvoiceStatusToResult(invoice, settings, out _);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var responseFields = new Dictionary<string, string>
        {
            ["x_account_id"] = settings.EpgAccountId,
            ["x_amount"] = invoiceData.Amount,
            ["x_currency"] = invoiceData.Currency,
            ["x_reference"] = invoiceData.OrderReference,
            ["x_result"] = result,
            ["x_timestamp"] = timestamp,
        };
        responseFields["x_signature"] = _jumpSellerService.ComputeSignature(responseFields, settings.EpgSecret);

        var qs = string.Join("&", responseFields.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var target = result == "completed" ? invoiceData.CompleteUrl : invoiceData.CancelUrl;
        var separator = target.Contains('?') ? '&' : '?';
        return Redirect($"{target}{separator}{qs}");
    }

    private string CheckoutUrl(string invoiceId) => Url.Action(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId }, Request.Scheme);
}
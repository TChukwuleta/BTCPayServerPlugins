using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Plugins.Salesforce.Services;
using BTCPayServer.Plugins.Salesforce.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Salesforce;

[AllowAnonymous]
[Route("~/plugins/{storeId}/salesforce/public/", Order = 0)]
[Route("~/plugins/{storeId}/salesforce/api/v1/", Order = 1)]
public class UIPublicSalesforceController : Controller
{
    private readonly StoreRepository _storeRepo;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;
    private readonly SalesforceDbContextFactory _dbContextFactory;
    public UIPublicSalesforceController
        (StoreRepository storeRepo,
        UIInvoiceController invoiceController,
        SalesforceDbContextFactory dbContextFactory,
        InvoiceRepository invoiceRepository)
    {
        _storeRepo = storeRepo;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }

    [HttpPost("create-invoice")]
    public async Task<IActionResult> CreateInvoice([FromRoute] string storeId, [FromBody] CreateInvoiceRequestVm model)
    {
        Console.WriteLine(JsonConvert.SerializeObject(model, Formatting.Indented));
        await using var ctx = _dbContextFactory.CreateContext();
        var salesforceSetting = ctx.SalesforceSettings.FirstOrDefault(c => c.StoreId == storeId);
        var store = await _storeRepo.FindStore(salesforceSetting?.StoreId);
        if (salesforceSetting == null || store == null || !salesforceSetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid BTCPay Store specified. Please contact the admin");
        }
        var salesforceSearchTerm = $"{SalesforceHostedService.SALESFORCE_ORDER_ID_PREFIX}{model.orderId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = salesforceSearchTerm,
            StoreId = new[] { salesforceSetting.StoreId }
        });
        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SalesforceHostedService.SALESFORCE_ORDER_ID_PREFIX).Any(s => s == model.orderId.ToString())).ToArray();

        var firstInvoice = matchedExistingInvoices.LastOrDefault(entity => new[] { "settled", "processing", "new" }.Contains(entity.GetInvoiceState().Status.ToString().ToLower()));
        try
        {
            if (firstInvoice != null) return ReturnResponse(firstInvoice);

            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = Decimal.Parse(model.amount),
                    Currency = model.currency,
                    Metadata = new JObject
                    {
                        ["cartId"] = model.cartId,
                        ["orderId"] = model.orderId,
                        ["checkoutId"] = model.checkoutId,
                        ["webstoreId"] = model.webstoreId,
                        ["orderReferenceNumber"] = model.orderId

                    },
                    AdditionalSearchTerms = new[]
                    {
                            model.orderId.ToString(CultureInfo.InvariantCulture),
                            salesforceSearchTerm
                    }
                }, store,
                Request.GetAbsoluteRoot(), new List<string>() { salesforceSearchTerm });
            Console.WriteLine(JsonConvert.SerializeObject(invoice, Formatting.Indented));
            return ReturnResponse(invoice);
        }
        catch (Exception ex)
        {
            return BadRequest($"An error occurred while trying to create invoice for salesforce. {ex.Message}");
        }
    }


    [HttpPost("invoices/{invoiceId}/mark-invalid")]
    public async Task<IActionResult> MarkInvoiceAsInvalid(string storeId, string invoiceId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var salesforceSetting = ctx.SalesforceSettings.FirstOrDefault(c => c.StoreId == storeId);
        var store = await _storeRepo.FindStore(salesforceSetting?.StoreId);
        if (salesforceSetting == null || store == null || !salesforceSetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid BTCPay Store specified. Please contact the admin");
        }
        var invoice = await _invoiceRepository.GetInvoice(invoiceId);
        if (invoice == null || invoice.StoreId != salesforceSetting.StoreId)
        {
            return NotFound("Invoice not found or does not belong to the specified store.");
        }
        var markAsInvalid = await _invoiceRepository.MarkInvoiceStatus(invoice.Id, InvoiceStatus.Invalid);
        if (!markAsInvalid)
        {
            return BadRequest("Failed to mark invoice as invalid.");
        }
        return Ok("Invoice successfully marked as invalid");
    }


    [HttpGet("invoices/{invoiceId}")]
    public async Task<IActionResult> GetInvoice(string storeId, string invoiceId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var salesforceSetting = ctx.SalesforceSettings.FirstOrDefault(c => c.StoreId == storeId);
        var store = await _storeRepo.FindStore(salesforceSetting?.StoreId);
        if (salesforceSetting == null || store == null || !salesforceSetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid BTCPay Store specified. Please contact the admin");
        }
        var invoice = await _invoiceRepository.GetInvoice(invoiceId);
        if (invoice == null || invoice.StoreId != salesforceSetting.StoreId)
        {
            return NotFound("Invoice not found or does not belong to the specified store.");
        }
        Console.WriteLine(JsonConvert.SerializeObject(invoice, Formatting.Indented));
        return ReturnResponse(invoice);
    }

    private IActionResult ReturnResponse(InvoiceEntity invoice)
    {
        return Ok(new
        {
            id = invoice.Id,
            status = InvoiceStatus.Invalid.ToString(),
            currency = invoice.Currency,
            amount = invoice.Price,
            checkoutLink = Url.Action(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId = invoice.Id }, Request.Scheme)
        });
    }


}

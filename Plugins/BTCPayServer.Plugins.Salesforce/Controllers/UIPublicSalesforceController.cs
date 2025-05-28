using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Controllers;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Invoices;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.Salesforce.Services;
using BTCPayServer.Plugins.Salesforce.ViewModels;
using System.Linq;
using BTCPayServer.Abstractions.Extensions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System;
using BTCPayServer.Client.Models;

namespace BTCPayServer.Plugins.Salesforce;

[AllowAnonymous]
[Route("~/plugins/{storeId}/salesforce/public/", Order = 0)]
[Route("~/plugins/{storeId}/salesforce/api/", Order = 1)]
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
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("create-invoice")]
    public async Task<IActionResult> CreateInvoice(string storeId, CreateInvoiceRequestVm model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var salesforceSetting = ctx.SalesforceSettings.FirstOrDefault(c => c.StoreId == storeId);
        var store = await _storeRepo.FindStore(salesforceSetting?.StoreId);
        if (salesforceSetting == null || store == null || !salesforceSetting.IntegratedAt.HasValue)
        {
            return BadRequest("Invalid BTCPay Store specified. Please contact the admin");
        }
        var shopifySearchTerm = $"{SalesforceHostedService.SALESFORCE_ORDER_ID_PREFIX}{model.orderId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { salesforceSetting.StoreId }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(SalesforceHostedService.SALESFORCE_ORDER_ID_PREFIX).Any(s => s == model.orderId.ToString())).ToArray();

        var firstInvoiceSettled =  matchedExistingInvoices.LastOrDefault(entity =>  new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(entity.GetInvoiceState().Status.ToString().ToLower()));
        try
        {
            if (firstInvoiceSettled != null)
            {
                return Ok(new
                {
                    invoiceId = firstInvoiceSettled.Id,
                    status = firstInvoiceSettled.Status.ToString().ToLowerInvariant(),
                    //externalPaymentLink = Url.Action("InitiatePayment", "UIShopify", new { invoiceId = firstInvoiceSettled.Id, shopName, orderId = shopifySearchTerm }, Request.Scheme)
                });
            }
            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = model.amount,
                    Currency = model.currency,
                    Metadata = new JObject
                    {
                        ["orderId"] = model.orderId,
                        ["redirectUrl"] = model.redirectUrl,
                        ["buyerEmail"] = model.buyerEmail,
                        ["orderType"] = model.orderType
                    },
                    AdditionalSearchTerms = new[]
                    {
                            model.orderId.ToString(CultureInfo.InvariantCulture),
                            model.redirectUrl,
                            shopifySearchTerm
                    }
                }, store,
                Request.GetAbsoluteRoot(), new List<string>() { shopifySearchTerm });
            return Ok(new
            {
                invoiceId = invoice.Id,
                status = invoice.Status.ToString().ToLowerInvariant(),
                //externalPaymentLink = Url.Action("InitiatePayment", "UIShopify", new { invoiceId = invoice.Id, shopName, orderId = shopifySearchTerm }, Request.Scheme)
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"An error occurred while trying to create invoice for salesforce. {ex.Message}");
        }
    }
}

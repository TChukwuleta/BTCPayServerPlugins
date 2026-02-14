using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Plugins.Saleor.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Saleor;

[AllowAnonymous]
[Route("~/plugins/{storeId}/saleor/public/", Order = 0)]
[Route("~/plugins/{storeId}/saleor/api/", Order = 1)]
public class UISaleorPublicAppController : Controller
{
    private readonly InvoiceRepository _invoiceRepository;
    private readonly StoreRepository _storeRepository;
    private readonly UIInvoiceController _invoiceController;
    public UISaleorPublicAppController(StoreRepository storeRepository, InvoiceRepository invoiceRepository, UIInvoiceController invoiceController)
    {
        _storeRepository = storeRepository;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }


    /*[HttpGet("export")]
    public IActionResult ExportStore(string storeId)
    {
        if (CurrentStore == null) return NotFound();
        return View(new ExportViewModel
        {
            StoreId = CurrentStore.Id,
            SelectedOptions = ExportViewModel.AllOptions
        });
    }*/


    static AsyncDuplicateLock OrderLocks = new AsyncDuplicateLock();
    [HttpPost("create-invoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvoice(string storeId, CreateInvoiceViewModel vm)
    {
        var store = await _storeRepository.FindStore(storeId);
        if (store == null) return NotFound();

        Console.WriteLine(JsonConvert.SerializeObject(vm, Formatting.Indented));
        var searchTerm = $"{Extensions.SALEOR_ORDER_ID_PREFIX}{vm.TransactionId}";
        var invoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = searchTerm,
            StoreId = new[] { storeId }
        });

        using var l = await OrderLocks.LockAsync(vm.TransactionId, CancellationToken.None);

        var orderInvoices = invoices.Where(e => e.GetSaleorOrderId() == vm.TransactionId).ToArray();
        var currentInvoice = orderInvoices.FirstOrDefault();
        if (currentInvoice != null) return Ok();

        InvoiceEntity invoice;
        try
        {
            invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = decimal.Parse(vm.Amount),
                    Currency = vm.Currency,
                    Metadata = new JObject
                    {
                        ["orderId"] = vm.TransactionId,
                        ["saleorMetaData"] = JToken.FromObject(vm.MetaData)
                    },
                    AdditionalSearchTerms =
                    [
                        searchTerm
                    ],
                    Checkout = new()
                    {
                        RedirectURL = vm.RedirectUrl
                    }
                }, store,
                Request.GetAbsoluteRoot(), [searchTerm], CancellationToken.None);
        }
        catch (BitpayHttpException e)
        {
            return BadRequest(e.Message);
        }
        return Ok();
    }


    private IActionResult RedirectToInvoiceCheckout(string invoiceId)
    {
        return RedirectToAction(nameof(UIInvoiceController.Checkout), "UIInvoice",
                    new { invoiceId });
    }

}

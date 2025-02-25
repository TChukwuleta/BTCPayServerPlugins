using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using BTCPayServer.Models;
using BTCPayServer.Services;
using System.Collections.Generic;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Controllers;
using Newtonsoft.Json.Linq;
using System.Globalization;
using BTCPayServer.Client.Models;
using BTCPayServer.Abstractions.Extensions;
using Microsoft.AspNetCore.Routing;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.SimpleTicketSales.Services;
using BTCPayServer.Plugins.SimpleTicketSales.Data;
using BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[AllowAnonymous]
[Route("~/plugins/{storeId}/ticket/api/")]
public class UISimpleTicketSalesPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    public UISimpleTicketSalesPublicController
        (EmailService emailService,
        UriResolver uriResolver,
        IFileService fileService,
        StoreRepository storeRepo,
        LinkGenerator linkGenerator,
        IHttpClientFactory clientFactory,
        ApplicationDbContextFactory context,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _context = context;
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _fileService = fileService;
        _emailService = emailService;
        _linkGenerator = linkGenerator;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }



    [HttpGet("event/{eventId}/register")]
    public async Task<IActionResult> EventRegistration(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostEvent = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ghostEvent == null || ghostEvent.EventDate <= DateTime.UtcNow)
            return NotFound();

        if (ghostEvent.HasMaximumCapacity)
        {
            var eventTickets = await ctx.TicketSalesEventTickets.AsNoTracking().CountAsync(c => c.StoreId == storeId && c.EventId == eventId
                    && c.PaymentStatus == SimpleTicketSales.Data.TransactionStatus.Settled.ToString());
            if (eventTickets >= ghostEvent.MaximumEventCapacity)
                return NotFound();
        }
        var storeData = await _storeRepo.FindStore(storeId);
        var getFile = ghostEvent.EventImageUrl == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ghostEvent.EventImageUrl);

        return View(new CreateEventTicketViewModel { 
            EventId = ghostEvent.Id, 
            StoreId = storeId,
            EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile)),
            EventDate = ghostEvent.EventDate,
            Description = ghostEvent.Description,
            EventTitle = ghostEvent.Title,
            StoreName = storeData?.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData?.GetStoreBlob()),
        });
    }

    [HttpPost("event/{eventId}/register")]
    public async Task<IActionResult> EventRegistration(string storeId, string eventId, CreateEventTicketViewModel vm)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostEvent = await ctx.TicketSalesEvents.AsNoTracking().SingleOrDefaultAsync(c => c.StoreId == storeId && c.Id == eventId);
        if (ghostEvent == null)
            return NotFound();
        
        if (ghostEvent.HasMaximumCapacity)
        {
            var eventTickets = await ctx.TicketSalesEventTickets.AsNoTracking().CountAsync(c => c.StoreId == storeId && c.EventId == eventId
                    && c.PaymentStatus == SimpleTicketSales.Data.TransactionStatus.Settled.ToString());
            if (eventTickets >= ghostEvent.MaximumEventCapacity)
                return NotFound();
        }

        var existingTicket = await ctx.TicketSalesEventTickets.SingleOrDefaultAsync(c => c.Email == vm.Email.Trim() && c.EventId == eventId && c.StoreId == storeId);
        if (existingTicket?.PaymentStatus == SimpleTicketSales.Data.TransactionStatus.Settled.ToString())
        {
            ModelState.AddModelError(nameof(vm.Email), $"A user with this email has already purchased a ticket. Please contact support");
            return View(vm);
        }

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var uid = existingTicket?.Id ?? Guid.NewGuid().ToString();
        var invoice = await CreateInvoiceAsync(store, $"{SimpleTicketSalesHostedService.TICKET_SALES_PREFIX}", uid, ghostEvent.Amount, ghostEvent.Currency, Request.GetAbsoluteRoot());
        if (existingTicket != null)
        {
            existingTicket.InvoiceId = invoice.Id;
        }
        else
        {
            ctx.TicketSalesEventTickets.Add(new TicketSalesEventTicket
            {
                StoreId = storeId,
                EventId = eventId,
                Name = vm.Name.Trim(),
                Amount = ghostEvent.Amount,
                Currency = ghostEvent.Currency,
                Email = vm.Email.Trim(),
                PaymentStatus = SimpleTicketSales.Data.TransactionStatus.New.ToString(),
                CreatedAt = DateTime.UtcNow,
                InvoiceId = invoice.Id
            });
        }
        await ctx.SaveChangesAsync();
        return View("InitiatePayment", new SimpleTicketSalesOrderViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            BTCPayServerUrl = Request.GetAbsoluteRoot(),
            InvoiceId = invoice.Id
        });
    }


    public async Task<InvoiceEntity> CreateInvoiceAsync(BTCPayServer.Data.StoreData store, string prefix, string txnId, decimal amount, string currency, string url)
    {
        var ghostSearchTerm = $"{prefix}{txnId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = ghostSearchTerm,
            StoreId = new[] { store.Id }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(ghostSearchTerm).Any(s => s == txnId.ToString())).ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        if (firstInvoiceSettled != null)
            return firstInvoiceSettled;

        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
            new CreateInvoiceRequest()
            {
                Amount = amount,
                Currency = currency,
                Metadata = new JObject
                {
                    ["TxnId"] = txnId
                },
                AdditionalSearchTerms = new[]
                {
                    txnId.ToString(CultureInfo.InvariantCulture),
                    ghostSearchTerm
                }
            }, store, url, new List<string>() { ghostSearchTerm });

        return invoice;
    }
}

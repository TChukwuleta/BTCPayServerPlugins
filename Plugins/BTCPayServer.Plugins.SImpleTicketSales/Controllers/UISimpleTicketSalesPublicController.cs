using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using System;
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
using Microsoft.AspNetCore.Cors;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[AllowAnonymous]
[Route("~/plugins/{storeId}/ticket/api/")]
public class UISimpleTicketSalesPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    public UISimpleTicketSalesPublicController
        (UriResolver uriResolver,
        IFileService fileService,
        EmailService emailService,
        StoreRepository storeRepo,
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
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }



    [HttpGet("event/{eventId}/register")]
    public async Task<IActionResult> EventRegistration(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null || ticketEvent.EventDate <= DateTime.UtcNow)
            return NotFound();

        if (ticketEvent.HasMaximumCapacity)
        {
            var eventTickets = await ctx.TicketSalesEventTickets.AsNoTracking().CountAsync(c => c.StoreId == storeId && c.EventId == eventId
                    && c.PaymentStatus == SimpleTicketSales.Data.TransactionStatus.Settled.ToString());
            if (eventTickets >= ticketEvent.MaximumEventCapacity)
                return NotFound();
        }
        var storeData = await _storeRepo.FindStore(storeId);
        var getFile = ticketEvent.EventImageUrl == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ticketEvent.EventImageUrl);

        return View(new CreateEventTicketViewModel { 
            EventId = ticketEvent.Id, 
            StoreId = storeId,
            Amount = ticketEvent.Amount,
            Currency = ticketEvent.Currency,
            EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile)),
            EventDate = ticketEvent.EventDate,
            Description = ticketEvent.Description,
            EventTitle = ticketEvent.Title,
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


    [HttpGet("btcpay-ticketsales.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        var fileContent = _emailService.GetEmbeddedResourceContent("Resources.js.btcpay_ticketsales.js");
        return Content(fileContent, "text/javascript");
    }

    public async Task<InvoiceEntity> CreateInvoiceAsync(Data.StoreData store, string prefix, string txnId, decimal amount, string currency, string url)
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

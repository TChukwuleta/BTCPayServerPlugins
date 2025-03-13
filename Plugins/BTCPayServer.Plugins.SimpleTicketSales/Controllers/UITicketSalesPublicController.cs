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
using TransactionStatus = BTCPayServer.Plugins.SimpleTicketSales.Data.TransactionStatus;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[AllowAnonymous]
[Route("~/plugins/{storeId}/ticket/public/")]
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
        var now = DateTime.UtcNow;
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null || ticketEvent.StartDate.Date > now.Date || (ticketEvent.EndDate.HasValue && ticketEvent.EndDate.Value.Date < now.Date))
        {
            return NotFound();
        }

        if (ticketEvent.HasMaximumCapacity)
        {
            var totalTicketsSold = ctx.Orders.AsNoTracking()
                .Where(c => c.StoreId == storeId && c.EventId == eventId && c.PaymentStatus == TransactionStatus.Settled.ToString())
                .SelectMany(c => c.Tickets).Count();

            if (totalTicketsSold >= ticketEvent.MaximumEventCapacity)
                return NotFound();
        }

        var storeData = await _storeRepo.FindStore(storeId);
        var getFile = ticketEvent.EventLogo == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ticketEvent.EventLogo);

        return View(new CreateEventTicketViewModel { 
            EventId = ticketEvent.Id, 
            StoreId = storeId,
            Amount = ticketEvent.Amount,
            Currency = ticketEvent.Currency,
            EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile)),
            EventDate = ticketEvent.StartDate,
            Description = ticketEvent.Description,
            EventTitle = ticketEvent.Title,
            StoreName = storeData?.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData?.GetStoreBlob()),
        });
    }


    [HttpPost("{eventId}/purchase")]
    public async Task<IActionResult> PurchaseTickets(string storeId, string eventId, [FromBody] TicketPurchaseRequestVm model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        // Confirm that there is at least one ticket available.. 

        if (ticketEvent.HasMaximumCapacity && ctx.Orders
               .AsNoTracking().Where(c => c.StoreId == storeId && c.EventId == eventId && c.PaymentStatus == TransactionStatus.Settled.ToString())
               .Sum(c => c.Tickets.Count) >= ticketEvent.MaximumEventCapacity)
        {
            return NotFound();
        }

        // Check if it has max capacity.. ensure that the number of tickets in the request model isn't more than max capacity considering already sold tickets.

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var now = DateTime.UtcNow;
        decimal totalAmount = 0;
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();

        var order = new Order
        {
            TxnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20)),
            EventId = eventId,
            StoreId = storeId,
            TotalAmount = model.TotalAmount,
            PaymentStatus = TransactionStatus.New.ToString(),
            CreatedAt = now
        };

        foreach (var ticketRequest in model.Tickets)
        {
            var ticketType = ticketTypes.FirstOrDefault(c => c.Id == ticketRequest.TicketTypeId);

            bool isValidTicketType = ticketType != null && ticketType.Quantity > ticketType.QuantitySold;

            var amount = isValidTicketType ? ticketType.Price : ticketEvent.Amount;
            totalAmount += amount;

            if (isValidTicketType)
                ticketType.QuantitySold += 1;

            var ticket = new Ticket
            {
                StoreId = storeId,
                EventId = eventId,
                TicketTypeId = ticketRequest.TicketTypeId,
                Amount = amount,
                Currency = ticketEvent.Currency,
                Name = ticketRequest.AttendeeName.Trim(),
                Email = ticketRequest.AttendeeEmail.Trim(),
                CreatedAt = now,
                PaymentStatus = TransactionStatus.New.ToString(),
                AccessLink = ticketEvent.Location
            };
            order.Tickets.Add(ticket);
        }

        order.TotalAmount = totalAmount;
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var invoice = await CreateInvoiceAsync(store, order, ticketEvent.Currency, Request.GetAbsoluteRoot(), model.RedirectUrl);
        order.InvoiceId = invoice.Id;
        order.InvoiceStatus = invoice.Status.ToString();
        ctx.Orders.Update(order);
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
        var userStore = ctx.Events.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        var fileContent = _emailService.GetEmbeddedResourceContent("Resources.js.btcpay_ticketsales.js");
        return Content(fileContent, "text/javascript");
    }

    public async Task<InvoiceEntity> CreateInvoiceAsync(Data.StoreData store, Order order, string currency, string url, string redirectUrl)
    {
        var ticketSalesSearchTerm = $"{SimpleTicketSalesHostedService.TICKET_SALES_PREFIX}{order.TxnId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = ticketSalesSearchTerm,
            StoreId = new[] { store.Id }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(ticketSalesSearchTerm).Any(s => s == order.TxnId.ToString())).ToArray();

        var settledInvoice =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        if (settledInvoice != null) return settledInvoice;

        var invoiceRequest = new CreateInvoiceRequest()
        {
            Amount = order.TotalAmount,
            Currency = currency,
            Metadata = new JObject
            {
                ["orderId"] = order.Id,
                ["TxnId"] = order.TxnId
            },
            AdditionalSearchTerms = new[]
                {
                    order.TxnId.ToString(CultureInfo.InvariantCulture),
                     order.Id.ToString(CultureInfo.InvariantCulture),
                    ticketSalesSearchTerm
                }
        };
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            invoiceRequest.Checkout = new()
            {
                RedirectURL = redirectUrl
            };
        }

        return await _invoiceController.CreateInvoiceCoreRaw(invoiceRequest, store, url, new List<string>() { ticketSalesSearchTerm });
    }
}

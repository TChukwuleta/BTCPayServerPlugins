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
using TransactionStatus = BTCPayServer.Plugins.SimpleTicketSales.Data.TransactionStatus;
using NBitcoin;
using NBitcoin.DataEncoders;
using BTCPayServer.Plugins.SimpleTicketSales.Helper.Extensions;
using BTCPayServer.Plugins.SimpleTicketSales;

namespace BTCPayServer.Plugins.ShopifyPlugin;

[AllowAnonymous]
[Route("~/plugins/{storeId}/ticket/public/")]
public class UITicketSalesPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    private const string SessionKeyOrder = "Ticket_Order_";
    public UITicketSalesPublicController
        (UriResolver uriResolver,
        IFileService fileService,
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
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }


    [HttpGet("event/{eventId}/summary")]
    public async Task<IActionResult> EventSummary(string storeId, string eventId)
    {
        var now = DateTime.UtcNow;
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var getFile = ticketEvent.EventLogo == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ticketEvent.EventLogo);
        Console.WriteLine(getFile);
        var imageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
        Console.WriteLine(imageUrl);
        return View(new EventSummaryViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            EventTitle = ticketEvent.Title,
            EventDate = ticketEvent.StartDate,
            EventId = ticketEvent.Id,
            EventImageUrl = imageUrl,
            Description = ticketEvent.Description,
            EventType = ticketEvent.EventType,
        });
    }


    [HttpGet("event/{eventId}/summary/tickets")]
    public async Task<IActionResult> EventTicket(string storeId, string eventId)
    {
        var now = DateTime.UtcNow;
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.TicketTypeState == SimpleTicketSales.Data.EntityState.Active && c.EventId == eventId).ToList();
        var storeData = await _storeRepo.FindStore(storeId);
        var getFile = ticketEvent.EventLogo == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ticketEvent.EventLogo);
        return View(new EventTicketPageViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency,
            EventDate = ticketEvent.StartDate,
            EventId = ticketEvent.Id,
            EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile)),
            Description = ticketEvent.Description,
            Location = ticketEvent.Location,
            EventType = ticketEvent.EventType,
            TicketTypes = ticketTypes.Select(tt =>
            {
                return new TicketTypeViewModel
                {
                    Name = tt.Name,
                    Price = tt.Price,
                    Description = tt.Description,
                    QuantityAvailable = tt.Quantity - tt.QuantitySold,
                    TicketTypeId = tt.Id,
                    EventId = tt.EventId
                };
            }).ToList()
        });
    }


    [HttpPost("save-event-tickets")]
    public async Task<IActionResult> SaveEventTickets(string storeId, string eventId, EventTicketPageViewModel model)
    {
        var eventKey = $"{SessionKeyOrder}{eventId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
        {
            return NotFound();
        }
        if (model.Tickets == null || !model.Tickets.Any(t => t.Quantity > 0))
        {
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
        }

        // Check if it has max capacity.. ensure that the number of tickets in the request model isn't more than max capacity considering already sold tickets.

        var newOrder = new TicketOrderViewModel
        {
            EventId = eventId,
            StoreId = storeId,
            IsStepOneComplete = true, // Move to Contact page
            Tickets = model.Tickets
        };
        HttpContext.Session.SetObject(eventKey, newOrder);
        return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId });
    }


    [HttpGet("event/{eventId}/summary/contact")]
    public async Task<IActionResult> EventContactDetails(string storeId, string eventId)
    {
        var eventKey = $"{SessionKeyOrder}{eventId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var order = HttpContext.Session.GetObject<TicketOrderViewModel>(eventKey);
        if (order == null || !order.Tickets.Any())
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        return View(new ContactInfoPageViewModel
        {
            EventId = eventId,
            StoreId = storeId,
            StoreName = store.StoreName,
            Currency = ticketEvent.Currency,
            Tickets = order.Tickets,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            ContactInfo = new List<TicketContactInfoViewModel> { new TicketContactInfoViewModel() },
        });
    }

    [HttpPost("save-contact-details")]
    public async Task<IActionResult> SaveContactDetails(string storeId, string eventId, ContactInfoPageViewModel model)
    {
        var now = DateTime.UtcNow;
        decimal totalAmount = 0;
        var tickets = new List<Ticket>();
        var eventKey = $"{SessionKeyOrder}{eventId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        if (model.ContactInfo == null || !model.ContactInfo.Any())
        {
            return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId });
        }
        var orderViewModel = HttpContext.Session.GetObject<TicketOrderViewModel>(eventKey);
        if (orderViewModel == null || !orderViewModel.Tickets.Any())
        {
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
        }
        orderViewModel.ContactInfo = model.ContactInfo;
        orderViewModel.IsStepTwoComplete = true; // Move to Payment step
        HttpContext.Session.SetObject(eventKey, model);

        // Check if it has max capacity.. ensure that the number of tickets in the request model isn't more than max capacity considering already sold tickets.

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (!ticketTypes.Any())
            return NotFound();

        var order = new Order
        {
            TxnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10)),
            EventId = eventId,
            StoreId = storeId,
            PaymentStatus = TransactionStatus.New.ToString(),
            CreatedAt = now
        };

        foreach (var ticketRequest in orderViewModel.Tickets)
        {
            var ticketType = ticketTypes.FirstOrDefault(c => c.Id == ticketRequest.TicketTypeId);
            totalAmount += (ticketType.Price * ticketRequest.Quantity);
            ticketType.QuantitySold += ticketRequest.Quantity;

            for (int i = 0; i < ticketRequest.Quantity; i++)
            {
                string ticketTxn = Encoders.Base58.EncodeData(RandomUtils.GetBytes(6));
                var ticket = new Ticket
                {
                    StoreId = storeId,
                    EventId = eventId,
                    TicketTypeId = ticketType.Id,
                    Amount = ticketType.Price,
                    Currency = ticketEvent.Currency,
                    FirstName = model.ContactInfo.First().FirstName.Trim(),
                    LastName = model.ContactInfo.First().LastName.Trim(),
                    Email = model.ContactInfo.First().Email.Trim(),
                    CreatedAt = now,
                    TxnNumber = ticketTxn,
                    TicketNumber = $"EVT-{eventId:D4}-{now:yyMMdd}-{ticketTxn}",
                    TicketTypeName = ticketType.Name,
                    PaymentStatus = TransactionStatus.New.ToString(),
                    Location = ticketEvent.Location
                };
                tickets.Add(ticket);
            }
        }
        order.Tickets = tickets;
        order.TotalAmount = totalAmount;
        ctx.Orders.Add(order);
        ctx.TicketTypes.UpdateRange(ticketTypes);
        await ctx.SaveChangesAsync();

        var invoice = await CreateInvoiceAsync(store, order, ticketEvent.Currency, Request.GetAbsoluteRoot(), string.Empty); // Include redirect url to Event model
        order.InvoiceId = invoice.Id;
        order.InvoiceStatus = invoice.Status.ToString();
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();
        return RedirectToInvoiceCheckout(invoice.Id);
    }

    private async Task<InvoiceEntity> CreateInvoiceAsync(Data.StoreData store, Order order, string currency, string url, string redirectUrl)
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

    private bool ValidateEvent(SimpleTicketSalesDbContext ctx, string storeId, string eventId)
    {
        var now = DateTime.UtcNow;
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null || ticketEvent.EventState == SimpleTicketSales.Data.EntityState.Disabled 
            || ticketEvent.StartDate.Date < now.Date || (ticketEvent.EndDate.HasValue && ticketEvent.EndDate.Value.Date < now.Date))
            return false;

        if (ticketEvent.HasMaximumCapacity)
        {
            var totalTicketsSold = ctx.Orders.AsNoTracking()
                .Where(c => c.StoreId == storeId && c.EventId == eventId && c.PaymentStatus == TransactionStatus.Settled.ToString())
                .SelectMany(c => c.Tickets).Count();

            if (totalTicketsSold >= ticketEvent.MaximumEventCapacity)
                return false;
        }
        return true;
    }

    private IActionResult RedirectToInvoiceCheckout(string invoiceId)
    {
        return RedirectToAction(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId });
    }
}

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
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using TransactionStatus = BTCPayServer.Plugins.SatoshiTickets.Data.TransactionStatus;
using NBitcoin;
using NBitcoin.DataEncoders;
using BTCPayServer.Plugins.SatoshiTickets.Helper.Extensions;
using BTCPayServer.Abstractions.Constants;
using QRCoder;
using Microsoft.AspNetCore.Cors;

namespace BTCPayServer.Plugins.SatoshiTickets;

[AllowAnonymous]
[Route("~/plugins/{storeId}/ticket/public/")]
public class UITicketSalesPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly TicketService _ticketService;
    private readonly EmailService _emailService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    private const string SessionKeyOrder = "Ticket_Order_";
    public UITicketSalesPublicController
        (UriResolver uriResolver,
        IFileService fileService,
        StoreRepository storeRepo,
        EmailService emailService,
        TicketService ticketService,
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
        _ticketService = ticketService;
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
        var imageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
        return View(new EventSummaryViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            EventTitle = ticketEvent.Title,
            EventDate = ticketEvent.StartDate,
            EndDate = ticketEvent.EndDate,
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

        var ticketTypes = ctx.TicketTypes.Where(c => c.TicketTypeState == SatoshiTickets.Data.EntityState.Active && c.EventId == eventId).ToList();
        var storeData = await _storeRepo.FindStore(storeId);
        return View(new EventTicketPageViewModel
        {
            StoreId = storeId,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency,
            EventDate = ticketEvent.StartDate,
            EventId = ticketEvent.Id,
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
        string txnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
        var eventKey = $"{SessionKeyOrder}{eventId}_{txnId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (!ValidateEvent(ctx, storeId, eventId) || !ticketTypes.Any())
            return NotFound();

        if (model.Tickets == null || !model.Tickets.Any(t => t.Quantity > 0))
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        foreach (var ticket in model.Tickets)
        {
            var ticketType = ticketTypes.FirstOrDefault(c => c.Id == ticket.TicketTypeId);
            if (ticketType == null || (ticketType.Quantity - ticketType.QuantitySold) < ticket.Quantity)
                return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
        }
        var newOrder = new TicketOrderViewModel
        {
            TxnId = txnId,
            EventId = eventId,
            StoreId = storeId,
            IsStepOneComplete = true, // Move to Contact page
            Tickets = model.Tickets
        };
        HttpContext.Session.SetObject(eventKey, newOrder);
        return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId, txnId });
    }


    [HttpGet("event/{eventId}/summary/contact")]
    public async Task<IActionResult> EventContactDetails(string storeId, string eventId, string txnId)
    {
        var eventKey = $"{SessionKeyOrder}{eventId}_{txnId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var order = HttpContext.Session.GetObject<TicketOrderViewModel>(eventKey);
        if (order == null || order.Tickets == null || !order.Tickets.Any())
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        return View(new ContactInfoPageViewModel
        {
            TxnId = txnId,
            EventId = eventId,
            StoreId = storeId,
            StoreName = store.StoreName,
            Currency = ticketEvent.Currency,
            Tickets = order.Tickets,
            EventTitle = ticketEvent.Title,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            ContactInfo = new List<TicketContactInfoViewModel> { new TicketContactInfoViewModel() },
        });
    }

    [HttpPost("save-contact-details")]
    public async Task<IActionResult> SaveContactDetails(string storeId, string eventId, ContactInfoPageViewModel model)
    {
        if (model == null)
            return NotFound();

        var now = DateTime.UtcNow;
        decimal totalAmount = 0;
        var tickets = new List<Ticket>();
        var eventKey = $"{SessionKeyOrder}{eventId}_{model.TxnId}";
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (!ValidateEvent(ctx, storeId, eventId) || !ticketTypes.Any())
            return NotFound();

        if (model.ContactInfo == null || !model.ContactInfo.Any())
            return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId, model.TxnId });

        var orderViewModel = HttpContext.Session.GetObject<TicketOrderViewModel>(eventKey);
        if (orderViewModel == null || !orderViewModel.Tickets.Any())
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        foreach (var ticket in orderViewModel.Tickets)
        {
            var ticketType = ticketTypes.FirstOrDefault(c => c.Id == ticket.TicketTypeId);
            if (ticketType == null || (ticketType.Quantity - ticketType.QuantitySold) < ticket.Quantity)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Quanity specified for {ticket.TicketTypeName} is more than number of tickets available";
                return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
            }
        }

        orderViewModel.ContactInfo = model.ContactInfo;
        orderViewModel.IsStepTwoComplete = true; // Move to Payment step
        HttpContext.Session.SetObject(eventKey, model);

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.AsNoTracking().FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var order = new Order
        {
            TxnId = model.TxnId,
            EventId = eventId,
            StoreId = storeId,
            Currency = ticketEvent.Currency,
            PaymentStatus = TransactionStatus.New.ToString(),
            CreatedAt = now,
            TotalAmount = 0,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        foreach (var ticketRequest in orderViewModel.Tickets)
        {
            var ticketType = ticketTypes.FirstOrDefault(c => c.Id == ticketRequest.TicketTypeId);
            totalAmount += (ticketType.Price * ticketRequest.Quantity);

            for (int i = 0; i < ticketRequest.Quantity; i++)
            {
                string ticketTxn = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
                var ticket = new Ticket
                {
                    StoreId = storeId,
                    EventId = eventId,
                    TicketTypeId = ticketType.Id,
                    Amount = ticketType.Price,
                    QRCodeLink = Url.Action(nameof(EventTicketDisplay), "UITicketSalesPublic", new { storeId, eventId, orderId = order.Id }, Request.Scheme),
                    FirstName = model.ContactInfo.First().FirstName.Trim(),
                    LastName = model.ContactInfo.First().LastName.Trim(),
                    Email = model.ContactInfo.First().Email.Trim(),
                    CreatedAt = now,
                    TxnNumber = ticketTxn,
                    TicketNumber = $"EVT-{eventId:D4}-{now:yyMMdd}-{ticketTxn}",
                    TicketTypeName = ticketType.Name,
                    PaymentStatus = TransactionStatus.New.ToString()
                };
                tickets.Add(ticket);
            }
        }
        order.Tickets = tickets;
        order.TotalAmount = totalAmount;
        var invoice = await CreateInvoiceAsync(store, order, ticketEvent.Currency, Request.GetAbsoluteRoot(), ticketEvent.RedirectUrl ?? string.Empty);
        order.InvoiceId = invoice.Id;
        order.InvoiceStatus = invoice.Status.ToString();
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId = invoice.Id });
    }

    [HttpGet("event/{eventId}/ticket/{orderId}/summary")]
    public async Task<IActionResult> EventTicketDisplay(string storeId, string eventId, string orderId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.FirstOrDefaultAsync(a => a.Id == storeId);
        if (store == null) return NotFound();

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets).FirstOrDefault(o => o.StoreId == storeId && o.EventId == eventId && o.Id == orderId);
        if (order == null || !order.Tickets.Any()) return NotFound();

        return View(new TicketViewModel
        {
            EventName = ticketEvent.Title,
            Location = ticketEvent.Location,
            StartDate = ticketEvent.StartDate,
            EndDate = ticketEvent.EndDate,
            PurchaseDate = order.PurchaseDate.Value,
            Tickets = order.Tickets.Select(t => new TicketListViewModel
            {
                FirstName = t.FirstName,
                LastName = t.LastName,
                Email = t.Email,
                Currency = order.Currency,
                Amount = t.Amount,
                TicketNumber = t.TicketNumber,
                TxnNumber = t.TxnNumber,
                TicketType = t.TicketTypeName,
                QrCodeUrl = GenerateQrCodeDataUrl(t.TicketNumber),
            }).ToList(),
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
        });
    }


    [HttpGet("satoshiticket/jsqr_min.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetQRScannerJs(string storeId)
    {
        var store = await _storeRepo.FindStore(storeId);
        if (store == null)
            return NotFound();

        return Content(_emailService.GetEmbeddedResourceContent("Resources.js.jsqr_min.js"), "text/javascript");
    }


    private async Task<InvoiceEntity> CreateInvoiceAsync(BTCPayServer.Data.StoreData store, Order order, string currency, string url, string redirectUrl)
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
        if (ticketEvent == null || ticketEvent.EventState == Data.EntityState.Disabled 
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

    private string GenerateQrCodeDataUrl(string content)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCodeAsPngByteArr)}";
    }
}

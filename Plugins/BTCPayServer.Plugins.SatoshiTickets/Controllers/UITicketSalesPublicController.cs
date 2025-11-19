using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Helper.Extensions;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;
using QRCoder;
using TransactionStatus = BTCPayServer.Plugins.SatoshiTickets.Data.TransactionStatus;

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
    private readonly UIInvoiceController _invoiceController;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    private const string SessionKeyOrder = "Ticket_Order_";
    public UITicketSalesPublicController
        (UriResolver uriResolver,
        IFileService fileService,
        StoreRepository storeRepo,
        EmailService emailService,
        TicketService ticketService,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        SimpleTicketSalesDbContextFactory dbContextFactory)
    {
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
        var storeData = await _storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        var getFile = ticketEvent.EventLogo == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), ticketEvent.EventLogo);
        var imageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
        return View(new EventSummaryViewModel
        {
            StoreId = storeId,
            StoreName = storeData.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData.GetStoreBlob()),
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
        var storeData = await _storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        var ticketTypes = ctx.TicketTypes.Where(t => t.EventId == eventId && t.TicketTypeState == Data.EntityState.Active)
                              .Select(t => new TicketTypeViewModel
                              {
                                  Name = t.Name,
                                  Price = t.Price,
                                  Description = t.Description,
                                  QuantityAvailable = t.Quantity - t.QuantitySold,
                                  TicketTypeId = t.Id,
                                  EventId = t.EventId
                              }).ToList();

        return View(new EventTicketPageViewModel
        {
            StoreId = storeId,
            StoreName = storeData.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData.GetStoreBlob()),
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency,
            EventDate = ticketEvent.StartDate,
            EventId = ticketEvent.Id,
            Description = ticketEvent.Description,
            Location = ticketEvent.Location,
            EventType = ticketEvent.EventType,
            TicketTypes = ticketTypes
        });
    }


    [HttpPost("save-event-tickets")]
    public async Task<IActionResult> SaveEventTickets(string storeId, string eventId, EventTicketPageViewModel model)
    {
        if (model.Tickets?.Any(t => t.Quantity > 0) != true)
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        var availableTicketTypes = ctx.TicketTypes.Where(t => t.EventId == eventId).ToDictionary(t => t.Id, t => t.Quantity - t.QuantitySold);
        foreach (var ticket in model.Tickets.Where(t => t.Quantity > 0))
        {
            if (!availableTicketTypes.TryGetValue(ticket.TicketTypeId, out var left) || left < ticket.Quantity)
                return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
        }
        string txnId = Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
        var sessionKey = $"{SessionKeyOrder}{eventId}_{txnId}";
        var newOrder = new TicketOrderViewModel
        {
            TxnId = txnId,
            EventId = eventId,
            StoreId = storeId,
            IsStepOneComplete = true, // Move to Contact page
            Tickets = model.Tickets
        };
        HttpContext.Session.SetObject(sessionKey, newOrder);
        return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId, txnId });
    }


    [HttpGet("event/{eventId}/summary/contact")]
    public async Task<IActionResult> EventContactDetails(string storeId, string eventId, string txnId)
    {
        var sessionKey = $"{SessionKeyOrder}{eventId}_{txnId}";
        var order = HttpContext.Session.GetObject<TicketOrderViewModel>(sessionKey);
        if (order?.Tickets?.Any() != true)
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        var storeData = await _storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext(); 
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        return View(new ContactInfoPageViewModel
        {
            TxnId = txnId,
            EventId = eventId,
            StoreId = storeId,
            StoreName = storeData.StoreName,
            Currency = ticketEvent.Currency,
            Tickets = order.Tickets,
            EventTitle = ticketEvent.Title,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData.GetStoreBlob()),
            ContactInfo = new List<TicketContactInfoViewModel> { new TicketContactInfoViewModel() },
        });
    }

    [HttpPost("save-contact-details")]
    public async Task<IActionResult> SaveContactDetails(string storeId, string eventId, ContactInfoPageViewModel model)
    {
        if (model == null)
            return NotFound();

        if (model?.ContactInfo is null or { Count: 0 })
            return RedirectToAction(nameof(EventContactDetails), new { storeId, eventId, txnId = model.TxnId });

        var storeData = await _storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

        var sessionKey = $"{SessionKeyOrder}{eventId}_{model.TxnId}";
        var orderViewModel = HttpContext.Session.GetObject<TicketOrderViewModel>(sessionKey);
        if (orderViewModel?.Tickets is null or { Count: 0 })
            return RedirectToAction(nameof(EventTicket), new { storeId, eventId });

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToDictionary(t => t.Id);
        if (!ValidateEvent(ctx, storeId, eventId))
            return NotFound();

        foreach (var ticket in orderViewModel.Tickets)
        {
            if (!ticketTypes.TryGetValue(ticket.TicketTypeId, out var tt) || tt.Quantity - tt.QuantitySold < ticket.Quantity)
            {
                TempData[WellKnownTempData.ErrorMessage] = $"Quantity for {ticket.TicketTypeName} is more than number of tickets available";
                return RedirectToAction(nameof(EventTicket), new { storeId, eventId });
            }
        }
        orderViewModel.ContactInfo = model.ContactInfo;
        orderViewModel.IsStepTwoComplete = true; // Move to Payment step
        HttpContext.Session.SetObject(sessionKey, orderViewModel);
        var now = DateTimeOffset.UtcNow;
        var tickets = new List<Ticket>();
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
            var ticketType = ticketTypes[ticketRequest.TicketTypeId];
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
        order.TotalAmount = tickets.Sum(t => t.Amount);
        var invoice = await CreateInvoice(storeData, order, ticketEvent.Currency, Request.GetAbsoluteRoot(), ticketEvent.RedirectUrl ?? string.Empty);
        order.InvoiceId = invoice.Id;
        order.InvoiceStatus = invoice.Status.ToString();
        ctx.Orders.Update(order);
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(UIInvoiceController.Checkout), "UIInvoice", new { invoiceId = invoice.Id });
    }

    [HttpGet("event/{eventId}/ticket/{orderId}/summary")]
    public async Task<IActionResult> EventTicketDisplay(string storeId, string eventId, string orderId)
    {
        var store = await _storeRepo.FindStore(storeId);
        if (store == null) return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets).FirstOrDefault(o => o.StoreId == storeId && o.EventId == eventId && o.Id == orderId);
        if (order?.Tickets?.Any() != true) return NotFound();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == storeId && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

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
        if (store == null) return NotFound();

        return Content(_emailService.GetEmbeddedResourceContent("Resources.js.jsqr_min.js"), "text/javascript");
    }


    private async Task<InvoiceEntity> CreateInvoice(BTCPayServer.Data.StoreData store, Order order, string currency, string url, string redirectUrl)
    {
        var ticketSalesSearchTerm = $"{SimpleTicketSalesHostedService.TICKET_SALES_PREFIX}{order.TxnId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = ticketSalesSearchTerm,
            StoreId = new[] { store.Id }
        });
        matchedExistingInvoices = matchedExistingInvoices.Where(entity => entity.GetInternalTags(ticketSalesSearchTerm).Any(s => s == order.TxnId.ToString())).ToArray();

        var settledInvoice = matchedExistingInvoices.LastOrDefault(entity => new[] { "settled", "processing", "confirmed", "paid", "complete" }.Contains(entity.GetInvoiceState().Status.ToString().ToLower()));
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
        if (ticketEvent == null || ticketEvent.EventState == Data.EntityState.Disabled || ticketEvent.StartDate.Date < now.Date || (ticketEvent.EndDate.HasValue && ticketEvent.EndDate.Value.Date < now.Date))
            return false;

        if (ticketEvent.HasMaximumCapacity)
        {
            var totalTicketsSold = ctx.Orders.AsNoTracking().Where(c => c.StoreId == storeId && c.EventId == eventId && c.PaymentStatus == TransactionStatus.Settled.ToString())
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

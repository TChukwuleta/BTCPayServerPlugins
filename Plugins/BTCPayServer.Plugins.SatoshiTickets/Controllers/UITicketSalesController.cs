using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System;
using System.Net.Http;
using BTCPayServer.Services.Mails;
using Microsoft.AspNetCore.Routing;
using BTCPayServer.Services.Apps;
using BTCPayServer.Client;
using BTCPayServer.Services;
using Microsoft.AspNetCore.Http;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using BTCPayServer.Controllers;
using System.Text;
using Microsoft.CodeAnalysis;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/ticketevent/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UITicketSalesController : Controller
{
    private readonly AppService _appService;
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly TicketService _ticketService;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UITicketSalesController
        (AppService appService,
        UriResolver uriResolver,
        IFileService fileService,
        StoreRepository storeRepo,
        EmailService emailService,
        TicketService ticketService,
        IHttpClientFactory clientFactory,
        EmailSenderFactory emailSenderFactory,
        BTCPayNetworkProvider networkProvider,
        SimpleTicketSalesDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _appService = appService;
        _uriResolver = uriResolver;
        _fileService = fileService;
        _emailService = emailService;
        _userManager = userManager;
        _ticketService = ticketService;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _emailSenderFactory = emailSenderFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();


    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId, bool expired)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var events = ctx.Events.Where(c => c.StoreId == CurrentStore.Id).ToList();
        var eventTickets = ctx.Tickets.Where(t => t.StoreId == CurrentStore.Id && t.PaymentStatus == TransactionStatus.Settled.ToString()).ToList();
        var eventsViewModel = events.Select(ticketEvent =>
        {
            return new SalesTicketsEventsListViewModel
            {
                Location = ticketEvent.Location,
                Id = ticketEvent.Id,
                Title = ticketEvent.Title,
                EventPurchaseLink = Url.Action("EventSummary", "UITicketSalesPublic", new { storeId = CurrentStore.Id, eventId = ticketEvent.Id }, Request.Scheme),
                Description = ticketEvent.Description,
                EventDate = ticketEvent.StartDate,
                CreatedAt = ticketEvent.CreatedAt,
                StoreId = CurrentStore.Id,
                EventState = ticketEvent.EventState,
                TicketSold = eventTickets.Count(c => c.EventId == ticketEvent.Id)
            };
        }).ToList();

        if (expired)
        {
            eventsViewModel = eventsViewModel.Where(c => c.EventDate <= DateTime.UtcNow).ToList();
        }

        var emailSender = await _emailSenderFactory.GetEmailSender(storeId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        ViewData["StoreEmailSettingsConfigured"] = isEmailSettingsConfigured;
        if (!isEmailSettingsConfigured)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"Kindly <a href='{Url.Action(nameof(UIStoresController.StoreEmailSettings), "UIStores", new { storeId = CurrentStore.Id })}' class='alert-link'>configure Email SMTP</a> to create an event",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
        }
        var vm = new SalesTicketsEventsViewModel { DisplayedEvents = eventsViewModel, Expired = expired };
        return View(vm);
    }


    [HttpGet("view")]
    public async Task<IActionResult> ViewEvent(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var defaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, string.Empty);
        var vm = new UpdateSimpleTicketSalesEventViewModel { StoreId = CurrentStore.Id, StoreDefaultCurrency = defaultCurrency };
        if (!string.IsNullOrEmpty(eventId))
        {
            var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
            if (entity == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Invalid event record specified for this store";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            vm = TicketSalesEventToViewModel(entity);
            var getFile = entity.EventLogo == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventLogo);
            vm.EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
            vm.StoreDefaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, entity.Currency);
        }
        vm.EventTypes = Enum.GetValues(typeof(EventType)).Cast<EventType>()
            .Select(e => new SelectListItem
            {
                Value = e.ToString(),
                Text = e.ToString()
            }).ToList();
        return View(vm);
    }


    [HttpPost("create")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromForm] UpdateSimpleTicketSalesEventViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        if (vm.HasMaximumCapacity && (!vm.MaximumEventCapacity.HasValue || vm.MaximumEventCapacity.Value <= 0))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Kindly input the event capacity";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
        }
        if (vm.StartDate <= DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event date cannot be in the past";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
        }
        if (vm.EndDate.HasValue && vm.EndDate.Value < vm.StartDate)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event end date cannot be before start date";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
        }
        var entity = TicketSalesEventViewModelToEntity(vm, null);
        entity.EventState = Data.EntityState.Disabled;
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, GetUserId());
            if (!imageUpload.Success)
            {
                TempData[WellKnownTempData.ErrorMessage] = imageUpload.Response;
                return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
            }
            else
            {
                entity.EventLogo = imageUpload.StoredFile.Id;
            }
        }
        entity.CreatedAt = DateTime.UtcNow;
        entity.Currency = await GetStoreDefaultCurrentIfEmpty(storeId, vm.Currency);
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event created successfully. Kindly create ticket tiers for your event to publish your event";
        return RedirectToAction(nameof(UITicketTypeController.List), "UITicketType", new { storeId = CurrentStore.Id, eventId = entity.Id });
    }


    [HttpPost("update/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, UpdateSimpleTicketSalesEventViewModel vm, [FromForm] bool RemoveEventLogoFile = false)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event record specified for this store";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        var ticketTiersCount = ctx.TicketTypes.Where(t => t.EventId == eventId).Sum(c => c.Quantity);
        if (vm.HasMaximumCapacity && vm.MaximumEventCapacity < ticketTiersCount)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Maximum capacity is less that the sum of all tiers capacity. Kindly increase capacity";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id, eventId });
        }
        if (vm.EndDate.HasValue && vm.EndDate.Value < vm.StartDate)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event end date cannot be before start date";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id, eventId });
        }
        entity = TicketSalesEventViewModelToEntity(vm, entity);
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, GetUserId());
            if (!imageUpload.Success)
            {
                TempData[WellKnownTempData.ErrorMessage] = imageUpload.Response;
                return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id, eventId });
            }
        }
        if (imageUpload?.Success is true)
        {
            entity.EventLogo = imageUpload.StoredFile.Id;
        }
        else if (RemoveEventLogoFile)
        {
            entity.EventLogo = null;
            vm.EventImageUrl = null;
        }
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event updated successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }

    [HttpGet("toggle/{eventId}")]
    public async Task<IActionResult> ToggleTicketEventStatus(string storeId, string eventId, bool enable)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null || !ticketTypes.Any())
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"No ticket tier available. Click <a href='{Url.Action(nameof(UITicketTypeController.List), "UITicketType", new { storeId = CurrentStore.Id, eventId })}' class='alert-link'>here</a> to create one",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        return View("Confirm", new ConfirmModel($"{(enable ? "Activate" : "Disable")} event", $"The event ({ticketEvent.Title}) will be {(enable ? "activated" : "disabled")}. Are you sure?", (enable ? "Activate" : "Disable")));
    }


    [HttpPost("toggle/{eventId}")]
    public async Task<IActionResult> ToggleTicketEventStatusPost(string storeId, string eventId, bool enable)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null || !ticketTypes.Any())
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"No ticket type available. Click <a href='{Url.Action(nameof(UITicketTypeController.List), "UITicketType", new { storeId = CurrentStore.Id, eventId })}' class='alert-link'>here</a> to create one",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        switch (ticketEvent.EventState)
        {
            case Data.EntityState.Active:
            default:
                ticketEvent.EventState = Data.EntityState.Disabled;
                break;
            case Data.EntityState.Disabled:
                ticketEvent.EventState = Data.EntityState.Active;
                break;
        }
        ctx.Events.Update(ticketEvent);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Event {(enable ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(List), new { storeId });
    }

    [HttpGet("delete/{eventId}")]
    public async Task<IActionResult> DeleteEvent(string storeId, string eventId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.Tickets.Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.StartDate > DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        return View("Confirm", new ConfirmModel($"Delete Event", $"All tickets associated with this Event: {entity.Title} would also be deleted. Are you sure?", "Delete Event"));
    }


    [HttpPost("delete/{eventId}")]
    public async Task<IActionResult> DeleteEventPost(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.Tickets.Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();    
        if (tickets.Any())
        {
            if (entity.StartDate > DateTime.UtcNow)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            else
            {
                ctx.Tickets.RemoveRange(tickets);
            }
        }
        if (ticketTypes.Any())
        {
            ctx.TicketTypes.RemoveRange(ticketTypes);
        }
        ctx.Events.Remove(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event deleted successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }

    [HttpGet("{eventId}/tickets")]
    public async Task<IActionResult> ViewEventTicket(string storeId, string eventId, string searchText)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Event specified";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        var ordersQuery = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .Where(c => c.EventId == eventId && c.StoreId == CurrentStore.Id && c.PaymentStatus == TransactionStatus.Settled.ToString());

        int ticketsBought = ordersQuery.SelectMany(c => c.Tickets).Count();
        int ticketsChecked = ordersQuery.SelectMany(c => c.Tickets).Count(c => c.UsedAt.HasValue);
        if (!string.IsNullOrEmpty(searchText))
        {
            ordersQuery = ordersQuery.Where(o =>
                o.InvoiceId.Contains(searchText) || o.Tickets.Any(t => t.TxnNumber.Contains(searchText) || t.FirstName.Contains(searchText) ||  t.LastName.Contains(searchText) || t.Email.Contains(searchText)));
        }  
        var orders = ordersQuery.ToList();
        var vm = new EventTicketViewModel
        {
            TicketsCount = ticketsBought,
            CheckedInTicketsCount = ticketsChecked,
            StoreId = storeId,
            EventId = eventId,
            EventTitle = entity.Title,
            SearchText = searchText,
            TicketOrders = orders.Select(o => new EventTicketOrdersVm
            {
                OrderId = o.Id,
                Quantity = o.Tickets.Count(c => c.PaymentStatus == TransactionStatus.Settled.ToString()),
                InvoiceId = o.InvoiceId,
                HasEmailNotificationBeenSent = o.EmailSent,
                FirstName = o.Tickets.First(c => c.PaymentStatus == TransactionStatus.Settled.ToString()).FirstName,
                LastName = o.Tickets.First(c => c.PaymentStatus == TransactionStatus.Settled.ToString()).LastName,
                Email = o.Tickets.First(c => c.PaymentStatus == TransactionStatus.Settled.ToString()).Email,
                PurchaseDate = o.PurchaseDate.Value,
                Tickets = o.Tickets.Select(t => new EventContactPersonTicketVm
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    TicketTypeId = t.TicketTypeId,
                    FirstName = t.FirstName,
                    LastName = t.LastName,
                    Email = t.Email,
                    TicketNumber = t.TxnNumber,
                    Currency = o.Currency,
                    CheckedIn = t.UsedAt.HasValue,
                    TicketTypeName = t.TicketTypeName,
                }).ToList(),
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet("{eventId}/tickets/{ticketNumber}/check-in")]
    public async Task<IActionResult> CheckinTicketAttendee(string storeId, string eventId, string ticketNumber)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        var checkinTicket = await _ticketService.CheckinTicket(eventId, ticketNumber, CurrentStore.Id);
        if (checkinTicket.Success)
        {
            TempData[WellKnownTempData.SuccessMessage] = $"Ticket for {checkinTicket.Ticket.FirstName} {checkinTicket.Ticket.LastName} of ticket type: {checkinTicket.Ticket.TicketTypeName} checked-in successfully";
        }
        else
        {
            TempData[WellKnownTempData.ErrorMessage] = checkinTicket.ErrorMessage;
        }
        return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
    }

    [HttpGet("{eventId}/send-reminder/{orderId}")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string orderId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id.Equals(eventId) && c.StoreId.Equals(CurrentStore.Id));
        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets).FirstOrDefault(o => o.Id == orderId && o.StoreId == CurrentStore.Id && o.EventId == eventId && o.Tickets.Any());
        if (ticketEvent == null || order == null || !order.Tickets.Any())
            return NotFound();

        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStore.Id);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Email settings not setup. Kindly configure Email SMTP in the admin settings";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }
        try
        {
            var emailResponse = await _emailService.SendTicketRegistrationEmail(CurrentStore.Id, order.Tickets.First(), ticketEvent);
            if (emailResponse.IsSuccessful)
                order.EmailSent = true;

            ctx.Orders.Update(order);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured when sending ticket details. {ex.Message}";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }
        TempData[WellKnownTempData.SuccessMessage] = $"Ticket details has been sent to recipients via email";
        return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
    }


    [HttpGet("{eventId}/export")]
    public async Task<IActionResult> ExportTickets(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        var ordersWithTickets = ctx.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.EventId == eventId && o.PaymentStatus == TransactionStatus.Settled.ToString())
            .SelectMany(o => o.Tickets.Select(t => new
            {
                o.PurchaseDate,
                t.TxnNumber,
                t.FirstName,
                t.LastName,
                t.Email,
                t.TicketTypeName,
                t.Amount,
                o.Currency,
                t.UsedAt
            })).ToList();
        if (ticketEvent == null || ordersWithTickets == null || !ordersWithTickets.Any())
            return NotFound();

        var fileName = $"{ticketEvent.Title}_Tickets-{DateTime.Now:yyyy_MM_dd-HH_mm_ss}.csv";
        var csvData = new StringBuilder();
        csvData.AppendLine("Purchase Date,Ticket Number,First Name,Last Name,Email,Ticket Tier,Amount,Currency,Attended Event");
        foreach (var ticket in ordersWithTickets)
        {
            csvData.AppendLine($"{ticket.PurchaseDate:MM/dd/yy HH:mm},{ticket.TxnNumber},{ticket.FirstName},{ticket.LastName},{ticket.Email},{ticket.TicketTypeName},{ticket.Amount},{ticket.Currency},{ticket.UsedAt.HasValue}");
        }
        byte[] fileBytes = Encoding.UTF8.GetBytes(csvData.ToString());
        return File(fileBytes, "text/csv", fileName);
    }

    private async Task<string> GetStoreDefaultCurrentIfEmpty(string storeId, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            var store = await _storeRepo.FindStore(storeId);
            currency = store.GetStoreBlob().DefaultCurrency;
        }
        return currency.Trim().ToUpperInvariant();
    }

    private string GetUserId() => _userManager.GetUserId(User);

    private UpdateSimpleTicketSalesEventViewModel TicketSalesEventToViewModel(Event entity)
    {
        return new UpdateSimpleTicketSalesEventViewModel
        {
            StoreId = entity.StoreId,   
            EventId = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Location = entity.Location,
            StartDate = entity.StartDate,
            Currency = entity.Currency,
            RedirectUrl = entity.RedirectUrl,
            EmailBody = entity.EmailBody,
            EndDate = entity.EndDate,
            EventType = entity.EventType,
            EmailSubject = entity.EmailSubject,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity
        };
    }

    private Event TicketSalesEventViewModelToEntity(UpdateSimpleTicketSalesEventViewModel model, Event entity)
    {
        void MapTo(Event e)
        {
            e.StoreId = CurrentStore.Id;
            e.Title = model.Title;
            e.Description = model.Description;
            e.Location = model.Location;
            e.StartDate = model.StartDate;
            e.EndDate = model.EndDate;
            e.Currency = model.Currency;
            e.EmailBody = model.EmailBody;
            e.EventType = model.EventType;
            e.RedirectUrl = model.RedirectUrl;
            e.EmailSubject = model.EmailSubject;
            e.HasMaximumCapacity = model.HasMaximumCapacity;
            e.MaximumEventCapacity = model.MaximumEventCapacity;

            if (!string.IsNullOrWhiteSpace(model.EventImageUrl))
                e.EventLogo = model.EventImageUrl;
        }

        if (entity is null)
        {
            var newEvent = new Event();
            MapTo(newEvent);
            return newEvent;
        }
        MapTo(entity);
        return entity;
    }
}

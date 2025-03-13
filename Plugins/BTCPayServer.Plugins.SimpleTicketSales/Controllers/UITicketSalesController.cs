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
using System.Collections.Generic;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.SimpleTicketSales.Data;
using BTCPayServer.Plugins.SimpleTicketSales.Services;
using BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ticketsales/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UITicketSalesController : Controller
{
    private readonly AppService _appService;
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly IHttpClientFactory _clientFactory;
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

        var events = ctx.Events.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id).ToList();

        var eventTickets = ctx.Tickets.AsNoTracking().Where(t => t.StoreId == CurrentStore.Id).ToList();
        var eventsViewModel = events.Select(ticketEvent =>
        {
            return new SalesTicketsEventsListViewModel
            {
                Id = ticketEvent.Id,
                Title = ticketEvent.Title,
                EventPurchaseLink = Url.Action("EventRegistration", "UISimpleTicketSalesPublic", new { storeId = CurrentStore.Id, eventId = ticketEvent.Id }, Request.Scheme),
                Description = ticketEvent.Description,
                EventDate = ticketEvent.StartDate,
                CreatedAt = ticketEvent.CreatedAt,
                StoreId = CurrentStore.Id,
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
                Message = $"Kindly configure Email SMTP in the admin settings to be able to create an event",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
        }
        return View(new SalesTicketsEventsViewModel { DisplayedEvents = eventsViewModel, Expired = expired });
    }


    [HttpGet("view-event")]
    public async Task<IActionResult> ViewEvent(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var defaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, string.Empty);
        var vm = new UpdateSimpleTicketSalesEventViewModel { StoreId = CurrentStore.Id, StoreDefaultCurrency = defaultCurrency };
        if (!string.IsNullOrEmpty(eventId))
        {
            var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
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
        return View(vm);
    }


    [HttpPost("create-event")]
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
        if (vm.EventDate <= DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event date cannot be in the past";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
        }
        if (vm.Amount <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Amount cannot be 0";
            return RedirectToAction(nameof(ViewEvent), new { storeId = CurrentStore.Id });
        }
        var entity = TicketSalesEventViewModelToEntity(vm);
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
        vm.EventImageFile = null;
        entity.CreatedAt = DateTime.UtcNow;
        entity.Currency = await GetStoreDefaultCurrentIfEmpty(storeId, vm.Currency);
        entity.StoreId = CurrentStore.Id;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event created successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }


    [HttpPost("update-event/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, UpdateSimpleTicketSalesEventViewModel vm, [FromForm] bool RemoveEventLogoFile = false)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event record specified for this store";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        if (vm.Amount <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Amount cannot be 0";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        entity = TicketSalesEventViewModelToEntity(vm);
        entity.Id = eventId;
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
            vm.EventImageUrl = null;
        }
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event updated successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }


    [HttpGet("delete-event/{eventId}")]
    public async Task<IActionResult> DeleteEvent(string storeId, string eventId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.Tickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.StartDate > DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        return View("Confirm", new ConfirmModel($"Delete Event", $"All tickets associated with this Event: {entity.Title} would also be deleted. Are you sure?", "Delete Event"));
    }


    [HttpPost("delete-event/{eventId}")]
    public async Task<IActionResult> DeleteEventPost(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.Tickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
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

        var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Event specified";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }

        /*var query = ctx.Tickets.AsNoTracking().Where(c => c.EventId == eventId && c.StoreId == CurrentStore.Id);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Email.ToLower().Contains(searchText) ||
                t.Name.ToLower().Contains(searchText) ||
                t.InvoiceId.ToLower().Contains(searchText));
        }
        var tickets = query.ToList();

        var vm = new EventTicketViewModel
        {
            StoreId = CurrentStore.Id,
            EventId = eventId,
            SearchText = searchText,
            EventTitle = entity.Title,
            Tickets = tickets.Select(t => new EventTicketVm
            {
                Id = t.Id,
                HasEmailNotificationBeenSent = t.EmailSent,
                CreatedDate = t.CreatedAt,
                Name = t.Name,
                Amount = t.Amount,
                Currency = t.Currency,
                Email = t.Email,
                TicketStatus = t.PaymentStatus,
                InvoiceId = t.InvoiceId
            }).ToList()
        };
        return View(vm);*/
        return View();
    }


    [HttpGet("{eventId}/send-reminder/{orderId}")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string orderId, string ticketId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id.Equals(eventId) && c.StoreId.Equals(CurrentStore.Id));
        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets).FirstOrDefault(o => o.Id == orderId && o.StoreId == CurrentStore.Id && o.EventId == eventId && o.Tickets.Any());
        if (ticketEvent == null || order == null)
            return NotFound();

        var emailSender = await _emailSenderFactory.GetEmailSender(CurrentStore.Id);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Email settings not setup. Kindly configure Email SMTP in the admin settings";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }

        var ticketReminder = !string.IsNullOrEmpty(ticketId) ? order.Tickets.Where(t => t.Id == ticketId) : order.Tickets;
        try
        {
            var emailResponse = await _emailService.SendTicketRegistrationEmail(CurrentStore.Id, ticketReminder.ToList(), ticketEvent);
            var failedRecipients = new HashSet<string>(emailResponse.FailedRecipients);
            foreach (var ticket in ticketReminder)
            {
                ticket.EmailSent = !failedRecipients.Contains(ticket.Email);
            }
            ctx.Orders.Update(order);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured when sending ticket details. {ex.Message}";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }
        TempData[WellKnownTempData.ErrorMessage] = $"Ticket details has been sent to recipients via email";
        return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
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
            EventLink = entity.Location,
            EventDate = entity.StartDate,
            Amount = entity.Amount,
            Currency = entity.Currency,
            EmailBody = entity.EmailBody,
            EmailSubject = entity.EmailSubject,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity
        };
    }

    private Event TicketSalesEventViewModelToEntity(UpdateSimpleTicketSalesEventViewModel model)
    {
        return new Event
        {
            StoreId = model.StoreId,
            Title = model.Title,
            Description = model.Description,
            EventLogo = model.EventImageUrl,
            Location = model.EventLink,
            StartDate = model.EventDate,
            Amount = model.Amount,
            Currency = model.Currency,
            EmailBody = model.EmailBody,
            EmailSubject = model.EmailSubject,
            HasMaximumCapacity = model.HasMaximumCapacity,
            MaximumEventCapacity = model.MaximumEventCapacity
        };
    }
}

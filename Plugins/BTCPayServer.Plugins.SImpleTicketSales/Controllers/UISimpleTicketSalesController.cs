using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Services.Stores;
using StoreData = BTCPayServer.Data.StoreData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using BTCPayServer.Services.Mails;
using Microsoft.AspNetCore.Routing;
using BTCPayServer.Services.Apps;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Services;
using BTCPayServer.Plugins.SimpleTicketSales.Services;
using BTCPayServer.Plugins.SimpleTicketSales.Data;
using BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ticketsales/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISimpleTicketSalesController : Controller
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
    public UISimpleTicketSalesController
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
    public async Task<IActionResult> List(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var events = ctx.TicketSalesEvents.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id).ToList();
        var eventTickets = ctx.TicketSalesEventTickets.AsNoTracking().Where(t => t.StoreId == CurrentStore.Id).ToList();

        var ghostEventsViewModel = events
           .Select(ticketEvent =>
           {
               var tickets = eventTickets.Where(t => t.EventId == ticketEvent.Id).ToList();
               return new SalesTicketsEventsListViewModel
               {
                   Id = ticketEvent.Id,
                   Title = ticketEvent.Title,
                   EventPurchaseLink = Url.Action("EventRegistration", "UISimpleTicketSalesPublic", new { storeId = CurrentStore.Id, eventId = ticketEvent.Id }, Request.Scheme),
                   Description = ticketEvent.Description,
                   EventDate = ticketEvent.EventDate,
                   CreatedAt = ticketEvent.CreatedAt,
                   StoreId = CurrentStore.Id,
                   Tickets = tickets.Select(t => new SalesTicketEventTicketsViewModel
                   {
                       Id = t.Id,
                       Name = t.Name,
                       StoreId = CurrentStore.Id,
                       EventId = ticketEvent.Id,
                       Amount = t.Amount,
                       Currency = t.Currency,
                       Email = t.Email,
                       InvoiceId = t.InvoiceId,
                       PaymentStatus = t.PaymentStatus,
                       PurchaseDate = t.PurchaseDate
                   }).ToList()
               };
           }).ToList();

        var emailSender = await _emailSenderFactory.GetEmailSender(storeId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        ViewData["StoreEmailSettingsConfigured"] = isEmailSettingsConfigured;
        if (!isEmailSettingsConfigured)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Kindly configure Email SMTP in the admin settings to be able to create an event",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
        }
        return View(new SalesTicketsEventsViewModel { DisplayedEvents = ghostEventsViewModel });
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
            var entity = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
            if (entity == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Invalid Ghost event record specified for this store";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            vm = TicketSalesEventToViewModel(entity);
            var getFile = entity.EventImageUrl == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventImageUrl);
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
                entity.EventImageUrl = imageUpload.StoredFile.Id;
            }
        }
        vm.EventImageFile = null;
        entity.CreatedAt = DateTime.UtcNow;
        entity.Currency = await GetStoreDefaultCurrentIfEmpty(storeId, vm.Currency);
        entity.StoreId = CurrentStore.Id;
        ctx.TicketSalesEvents.Update(entity);
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

        var entity = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Ghost event record specified for this store";
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
            entity.EventImageUrl = imageUpload.StoredFile.Id;
        }
        else if (RemoveEventLogoFile)
        {
            entity.EventImageUrl = null;
            vm.EventImageUrl = null;
            vm.EventImageUrl = null;
        }
        ctx.TicketSalesEvents.Update(entity);
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

        var entity = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.TicketSalesEventTickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.EventDate > DateTime.UtcNow)
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

        var entity = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.TicketSalesEventTickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        if (tickets.Any())
        {
            if (entity.EventDate > DateTime.UtcNow)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            else
            {
                ctx.TicketSalesEventTickets.RemoveRange(tickets);
            }
        }
        ctx.TicketSalesEvents.Remove(entity);
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

        var entity = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Event specified";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }

        var query = ctx.TicketSalesEventTickets.AsNoTracking().Where(c => c.EventId == eventId && c.StoreId == CurrentStore.Id);
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
        return View(vm);
    }


    [HttpGet("{eventId}/send-reminder/{ticketId}")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string ticketId)
    {

        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();

        var ghostEvent = ctx.TicketSalesEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        var eventTicket = ctx.TicketSalesEventTickets.AsNoTracking().FirstOrDefault(c => c.Id == ticketId && c.EventId == eventId);
        if (ghostEvent == null || eventTicket == null)
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
            await _emailService.SendTicketRegistrationEmail(CurrentStore.Id, eventTicket, ghostEvent);
            eventTicket.EmailSent = true;
            ctx.TicketSalesEventTickets.Update(eventTicket);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured when sending subscription reminder. {ex.Message}";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }
        TempData[WellKnownTempData.ErrorMessage] = $"Ticket details has been sent to {eventTicket.Name}";
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

    private UpdateSimpleTicketSalesEventViewModel TicketSalesEventToViewModel(TicketSalesEvent entity)
    {
        return new UpdateSimpleTicketSalesEventViewModel
        {
            StoreId = entity.StoreId,   
            EventId = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            EventLink = entity.EventLink,
            EventDate = entity.EventDate,
            Amount = entity.Amount,
            Currency = entity.Currency,
            EmailBody = entity.EmailBody,
            EmailSubject = entity.EmailSubject,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity
        };
    }

    private TicketSalesEvent TicketSalesEventViewModelToEntity(UpdateSimpleTicketSalesEventViewModel model)
    {
        return new TicketSalesEvent
        {
            StoreId = model.StoreId,
            Title = model.Title,
            Description = model.Description,
            EventImageUrl = model.EventImageUrl,
            EventLink = model.EventLink,
            EventDate = model.EventDate,
            Amount = model.Amount,
            Currency = model.Currency,
            EmailBody = model.EmailBody,
            EmailSubject = model.EmailSubject,
            HasMaximumCapacity = model.HasMaximumCapacity,
            MaximumEventCapacity = model.MaximumEventCapacity
        };
    }
}

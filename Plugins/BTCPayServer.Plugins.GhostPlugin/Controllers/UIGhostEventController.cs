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
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Mails;
using Microsoft.AspNetCore.Routing;
using BTCPayServer.Services.Apps;
using BTCPayServer.Client;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Services;
using System;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ghost/event/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostEventController : Controller
{
    private GhostHelper helper;
    private readonly AppService _appService;
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIGhostEventController
        (AppService appService,
        UriResolver uriResolver,
        IFileService fileService,
        EmailService emailService,
        StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        EmailSenderFactory emailSenderFactory,
        BTCPayNetworkProvider networkProvider,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _appService = appService;
        _uriResolver = uriResolver;
        _fileService = fileService;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        helper = new GhostHelper(_appService);
        _emailSenderFactory = emailSenderFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

        var events = ctx.GhostEvents.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();
        var eventTickets = ctx.GhostEventTickets.AsNoTracking().Where(t => t.StoreId == ghostSetting.StoreId).ToList();

        var ghostEventsViewModel = events
           .Select(ghostEvent =>
           {
               var tickets = eventTickets.Where(t => t.EventId == ghostEvent.Id).ToList();
               return new GhostEventsListViewModel
               {
                   Id = ghostEvent.Id,
                   Title = ghostEvent.Title,
                   EventPurchaseLink = Url.Action("EventRegistration", "UIGhostPublic", new { storeId = CurrentStore.Id, eventId = ghostEvent.Id }, Request.Scheme),
                   Description = ghostEvent.Description,
                   EventDate = ghostEvent.EventDate,
                   CreatedAt = ghostEvent.CreatedAt,
                   StoreId = CurrentStore.Id,
                   Tickets = tickets.Select(t => new GhostEventTicketsViewModel
                   {
                       Id = t.Id,
                       Name = t.Name,
                       StoreId = CurrentStore.Id,
                       EventId = ghostEvent.Id,
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
        return View(new GhostEventsViewModel { DisplayedEvents = ghostEventsViewModel });
    }

    [HttpGet("{eventId}/tickets")]
    public async Task<IActionResult> ViewEventTicket(string storeId, string eventId, string searchText)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == ghostSetting.StoreId);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Event specified";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }

        var query = ctx.GhostEventTickets.AsNoTracking().Where(c => c.EventId == eventId && c.StoreId == ghostSetting.StoreId);
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


    [HttpGet("view-event")]
    public async Task<IActionResult> ViewEvent(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

        var defaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, string.Empty);
        var vm = new UpdateGhostEventViewModel { StoreId = CurrentStore.Id, StoreDefaultCurrency = defaultCurrency };
        if (!string.IsNullOrEmpty(eventId))
        {
            var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == ghostSetting.StoreId);
            if (entity == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Invalid Ghost event record specified for this store";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            vm = helper.GhostEventToViewModel(entity);
            var getFile = entity.EventImageUrl == null ? null : await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventImageUrl);
            vm.EventImageUrl = getFile == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
            vm.StoreDefaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, entity.Currency);
        }
        return View(vm);
    }


    [HttpPost("create-event")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromForm] UpdateGhostEventViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

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
        var entity = helper.GhostEventViewModelToEntity(vm);
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, ghostSetting.ApplicationUserId);
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
        entity.Currency = await GetStoreDefaultCurrentIfEmpty(storeId, vm.Currency);
        entity.StoreId = CurrentStore.Id;
        ctx.GhostEvents.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event created successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }


    [HttpPost("update-event/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, UpdateGhostEventViewModel vm, [FromForm] bool RemoveEventLogoFile = false)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == ghostSetting.StoreId);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Ghost event record specified for this store";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        entity = helper.GhostEventViewModelToEntity(vm);
        entity.Id = eventId;
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, ghostSetting.ApplicationUserId);
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
        ctx.GhostEvents.Update(entity);
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
        var ghostSettingExists = await ctx.GhostSettings.AnyAsync(c => c.StoreId == CurrentStore.Id);
        if (!ghostSettingExists)
            return NoGhostSetupResult(storeId);

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.GhostEventTickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
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
        var ghostSettingExists = await ctx.GhostSettings.AnyAsync(c => c.StoreId == CurrentStore.Id);
        if (!ghostSettingExists)
            return NoGhostSetupResult(storeId);

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.GhostEventTickets.AsNoTracking().Where(c => c.StoreId == CurrentStore.Id && c.EventId == eventId).ToList();
        if (tickets.Any())
        {
            if (entity.EventDate > DateTime.UtcNow)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
                return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
            }
            else
            {
                ctx.GhostEventTickets.RemoveRange(tickets);
            }
        }
        ctx.GhostEvents.Remove(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event deleted successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }


    [HttpGet("{eventId}/send-reminder/{ticketId}")]
    public async Task<IActionResult> SendReminder(string storeId, string eventId, string ticketId)
    {

        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
            return NoGhostSetupResult(storeId);

        var ghostEvent = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        var eventTicket = ctx.GhostEventTickets.AsNoTracking().FirstOrDefault(c => c.Id == ticketId && c.EventId == eventId);
        if (ghostEvent == null || eventTicket == null)
            return NotFound();

        var emailSender = await _emailSenderFactory.GetEmailSender(ghostSetting.StoreId);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Email settings not setup. Kindly configure Email SMTP in the admin settings";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = CurrentStore.Id, eventId });
        }
        try
        {
            await _emailService.SendTicketRegistrationEmail(ghostSetting.StoreId, eventTicket, ghostEvent);
            eventTicket.EmailSent = true;
            ctx.GhostEventTickets.Update(eventTicket);
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

    public IActionResult NoGhostSetupResult(string storeId)
    {
        TempData.SetStatusMessageModel(new StatusMessageModel
        {
            Severity = StatusMessageModel.StatusSeverity.Error,
            Html = $"To manage ghost events, you need to set up Ghost credentials first",
            AllowDismiss = false
        });
        return RedirectToAction(nameof(UIGhostController.Index), "UIGhost", new { storeId });
    }
}

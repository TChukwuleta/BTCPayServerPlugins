using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
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
using BTCPayServer.Plugins.SimpleTicketSales.Data;
using BTCPayServer.Plugins.SimpleTicketSales.Services;
using BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ticketsales/{eventId}/tickettype/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UITicketTypeController : Controller
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
    public UITicketTypeController
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
    public async Task<IActionResult> List(string storeId, string eventId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == ticketEvent.Id).ToList();
        var tickets = ticketTypes.Select(x =>
        {
            return new TicketTypeViewModel
            {
                Name = x.Name,
                Price = x.Price,
                Quantity = x.Quantity,
                QuantitySold = x.QuantitySold,
                EventId = x.EventId,
                TicketTypeState = x.TicketTypeState,
                Description = x.Description,
            };
        }).ToList();
        return View(tickets);
    }


    [HttpGet("view")]
    public async Task<IActionResult> ViewTicketType(string storeId, string eventId)
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


    [HttpGet("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleUserStatus(string storeId, string eventId, string ticketTypeId, bool enable)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        return View("Confirm", new ConfirmModel($"{(enable ? "Activate" : "Disable")} user", $"The ticket type ({ticketType.Name}) will be {(enable ? "activated" : "disabled")}. Are you sure?", (enable ? "Activate" : "Disable")));
    }


    [HttpPost("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleUserStatusPost(string storeId, string eventId, string ticketTypeId, bool enable)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        switch (ticketType.TicketTypeState)
        {
            case SimpleTicketSales.Data.EntityState.Disabled:
                ticketType.TicketTypeState = SimpleTicketSales.Data.EntityState.Active;
                break;
            case SimpleTicketSales.Data.EntityState.Active:
                ticketType.TicketTypeState = SimpleTicketSales.Data.EntityState.Disabled;
                break;
            default:
                break;
        }
        ctx.TicketTypes.Update(ticketType);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Ticket type {(enable ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }


    [HttpGet("delete/{ticketTypeId}")]
    public async Task<IActionResult> DeleteTicketType(string storeId, string eventId, string ticketTypeId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        return View("Confirm", new ConfirmModel($"Delete Ticket Type", $"Ticket type: {ticketType.Name} would also be deleted. Are you sure?", "Delete ticket type"));
    }


    [HttpPost("delete/{ticketTypeId}")]
    public async Task<IActionResult> DeleteTicketTypePost(string storeId, string eventId, string ticketTypeId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        ctx.TicketTypes.Remove(ticketType);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Ticket type deleted successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }








    /*[HttpPost("create-event")]
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
    }*/


    /*[HttpPost("update-event/{eventId}")]
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
    }*/




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

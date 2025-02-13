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
using NBitcoin.DataEncoders;
using NBitcoin;

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
    private readonly IHttpClientFactory _clientFactory;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIGhostEventController
        (AppService appService,
        UriResolver uriResolver,
        IFileService fileService,
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
        var events = ctx.GhostEvents.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();

        var ghostEventsViewModel = events
           .Select(ghostEvent =>
           {
               return new GhostEventsListViewModel
               {
                   Id = ghostEvent.Id,
                   Title = ghostEvent.Title,
                   Description = ghostEvent.Description,
                   EventDate = ghostEvent.EventDate,
                   CreatedAt = ghostEvent.CreatedAt,
                   StoreId = CurrentStore.Id
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
        Console.WriteLine(JsonConvert.SerializeObject(ghostEventsViewModel));
        return View(new GhostEventsViewModel { DisplayedEvents = ghostEventsViewModel });
    }


    [HttpGet("{eventId}")]
    public async Task<IActionResult> GetEvent(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "No Ghost event record found for this Store";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        var vm = helper.GhostEventToViewModel(entity);
        vm.EventImageUrl = entity.EventImageUrl == null ? null : await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), entity.EventImageUrl);
        vm.StoreDefaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, entity.Currency);
        return View(vm);
    }


    [HttpGet("create-event")]
    public async Task<IActionResult> CreateEvent(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var defaultCurrency = await GetStoreDefaultCurrentIfEmpty(storeId, string.Empty);
        return View(new UpdateGhostEventViewModel { StoreId = CurrentStore.Id, StoreDefaultCurrency = defaultCurrency });
    }


    [HttpPost("create-event")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromForm] UpdateGhostEventViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var entity = helper.GhostEventViewModelToEntity(vm);
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, ghostSetting.ApplicationUserId);
            if (!imageUpload.Success)
            {
                ModelState.AddModelError(nameof(vm.EventImageFile), imageUpload.Response);
            }
            else
            {
                entity.EventImageUrl = new UnresolvedUri.FileIdUri(imageUpload.StoredFile.Id);
            }
        }
        if (!ModelState.IsValid)
        {
            return View(vm);
        }
        entity.Id = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20));
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
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var entity = ctx.GhostEvents.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == ghostSetting.StoreId);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Ghost event record specified for this store";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        entity = helper.GhostEventViewModelToEntity(vm);
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await _fileService.UploadImage(vm.EventImageFile, ghostSetting.ApplicationUserId);
            if (!imageUpload.Success)
            {
                ModelState.AddModelError(nameof(vm.EventImageFile), imageUpload.Response);
            }
        }
        if (!ModelState.IsValid)
        {
            return View(vm);
        }
        if (imageUpload?.Success is true)
        {
            entity.EventImageUrl = new UnresolvedUri.FileIdUri(imageUpload.StoredFile.Id);
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

    private async Task<string> GetStoreDefaultCurrentIfEmpty(string storeId, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            var store = await _storeRepo.FindStore(storeId);
            if (store == null)
            {
                throw new Exception($"Could not find store with id {storeId}");
            }
            currency = store.GetStoreBlob().DefaultCurrency;
        }
        return currency.Trim().ToUpperInvariant();
    }

}

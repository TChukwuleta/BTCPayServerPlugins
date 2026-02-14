using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Models.Api;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

/// <summary>
/// Greenfield API controller for managing SatoshiTickets events.
/// Provides CRUD operations, status toggling, and logo upload/delete.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketsEventsController : ControllerBase
{
    private readonly StoreRepository _storeRepo;
    private readonly UriResolver _uriResolver;
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;

    public GreenfieldSatoshiTicketsEventsController(
        StoreRepository storeRepo,
        UriResolver uriResolver,
        IFileService fileService,
        UserManager<ApplicationUser> userManager,
        SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _fileService = fileService;
        _userManager = userManager;
        _dbContextFactory = dbContextFactory;
    }

    private string CurrentStoreId => HttpContext.GetStoreData()?.Id;

    /// <summary>
    /// List all events for a store. Optionally filter to expired events only.
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/satoshi-tickets/events")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> GetEvents(string storeId, [FromQuery] bool expired = false)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var eventsQuery = ctx.Events.Where(c => c.StoreId == CurrentStoreId);
        var events = eventsQuery.ToList();
        var eventTickets = ctx.Tickets
            .Where(t => t.StoreId == CurrentStoreId && t.PaymentStatus == TransactionStatus.Settled.ToString())
            .ToList();

        var result = new System.Collections.Generic.List<EventData>();
        foreach (var e in events)
        {
            result.Add(await ToEventData(e, eventTickets.Count(t => t.EventId == e.Id)));
        }

        if (expired)
        {
            result = result.Where(e => e.StartDate <= DateTime.UtcNow).ToList();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a single event by ID.
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> GetEvent(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == CurrentStoreId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(await ToEventData(entity, ticketsSold));
    }

    /// <summary>
    /// Create a new event. Events are created in Disabled state.
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/satoshi-tickets/events")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromBody] CreateEventRequest request)
    {
        if (request == null)
        {
            ModelState.AddModelError(nameof(request), "Request body is required");
            return this.CreateValidationError(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
            ModelState.AddModelError(nameof(request.Title), "Title is required");
        if (request.StartDate <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(request.StartDate), "Event date cannot be in the past");
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            ModelState.AddModelError(nameof(request.EndDate), "Event end date cannot be before start date");
        if (request.HasMaximumCapacity && (!request.MaximumEventCapacity.HasValue || request.MaximumEventCapacity.Value <= 0))
            ModelState.AddModelError(nameof(request.MaximumEventCapacity), "Maximum event capacity must be greater than zero when capacity is enabled");
        if (!string.IsNullOrEmpty(request.EventType) && !Enum.TryParse<EventType>(request.EventType, true, out _))
            ModelState.AddModelError(nameof(request.EventType), "Invalid event type. Valid values: Virtual, Physical");

        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        var currency = request.Currency;
        if (string.IsNullOrWhiteSpace(currency))
        {
            var store = await _storeRepo.FindStore(storeId);
            currency = store?.GetStoreBlob()?.DefaultCurrency ?? "USD";
        }

        var entity = new Event
        {
            StoreId = CurrentStoreId,
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Currency = currency.Trim().ToUpperInvariant(),
            RedirectUrl = request.RedirectUrl,
            EmailSubject = request.EmailSubject,
            EmailBody = request.EmailBody,
            EventType = Enum.TryParse<EventType>(request.EventType, true, out var et) ? et : EventType.Physical,
            HasMaximumCapacity = request.HasMaximumCapacity,
            MaximumEventCapacity = request.MaximumEventCapacity,
            EventState = Data.EntityState.Disabled,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrEmpty(request.EventLogoFileId))
            entity.EventLogo = request.EventLogoFileId;

        await using var ctx = _dbContextFactory.CreateContext();
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvent), new { storeId, eventId = entity.Id }, await ToEventData(entity, 0));
    }

    /// <summary>
    /// Update an existing event.
    /// </summary>
    [HttpPut("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, [FromBody] UpdateEventRequest request)
    {
        if (request == null)
        {
            ModelState.AddModelError(nameof(request), "Request body is required");
            return this.CreateValidationError(ModelState);
        }

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
            ModelState.AddModelError(nameof(request.Title), "Title is required");
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            ModelState.AddModelError(nameof(request.EndDate), "Event end date cannot be before start date");
        if (request.HasMaximumCapacity)
        {
            var ticketTiersCount = ctx.TicketTypes.Where(t => t.EventId == eventId).Sum(c => c.Quantity);
            if (request.MaximumEventCapacity < ticketTiersCount)
                ModelState.AddModelError(nameof(request.MaximumEventCapacity),
                    "Maximum capacity is less than the sum of all tiers capacity");
        }
        if (!string.IsNullOrEmpty(request.EventType) && !Enum.TryParse<EventType>(request.EventType, true, out _))
            ModelState.AddModelError(nameof(request.EventType), "Invalid event type. Valid values: Virtual, Physical");

        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Location = request.Location;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.RedirectUrl = request.RedirectUrl;
        entity.EmailSubject = request.EmailSubject;
        entity.EmailBody = request.EmailBody;
        entity.HasMaximumCapacity = request.HasMaximumCapacity;
        entity.MaximumEventCapacity = request.MaximumEventCapacity;

        if (!string.IsNullOrEmpty(request.Currency))
            entity.Currency = request.Currency.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(request.EventType))
            entity.EventType = Enum.Parse<EventType>(request.EventType, true);

        if (request.EventLogoFileId != null)
        {
            entity.EventLogo = string.IsNullOrEmpty(request.EventLogoFileId)
                ? null
                : request.EventLogoFileId;
        }

        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == CurrentStoreId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(await ToEventData(entity, ticketsSold));
    }

    /// <summary>
    /// Delete an event and its ticket types and tickets (if event is past).
    /// </summary>
    [HttpDelete("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}")]
    public async Task<IActionResult> DeleteEvent(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        var tickets = ctx.Tickets.Where(c => c.StoreId == CurrentStoreId && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.StartDate > DateTime.UtcNow)
        {
            return this.CreateAPIError(422, "event-has-active-tickets",
                "Cannot delete event as there are active ticket purchases and the event is in the future");
        }

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (tickets.Any())
            ctx.Tickets.RemoveRange(tickets);
        if (ticketTypes.Any())
            ctx.TicketTypes.RemoveRange(ticketTypes);

        ctx.Events.Remove(entity);
        await ctx.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    /// Toggle event status between Active and Disabled.
    /// </summary>
    [HttpPut("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/toggle")]
    public async Task<IActionResult> ToggleEventStatus(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (!ticketTypes.Any() && entity.EventState == Data.EntityState.Disabled)
        {
            return this.CreateAPIError(422, "no-ticket-types",
                "Cannot activate event without ticket types. Create at least one ticket type first.");
        }

        entity.EventState = entity.EventState == Data.EntityState.Active
            ? Data.EntityState.Disabled
            : Data.EntityState.Active;

        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == CurrentStoreId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(await ToEventData(entity, ticketsSold));
    }

    /// <summary>
    /// Upload an image as the event logo. Accepts multipart/form-data.
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/logo")]
    public async Task<IActionResult> UploadEventLogo(string storeId, string eventId, IFormFile file)
    {
        if (file == null)
        {
            ModelState.AddModelError(nameof(file), "No file was uploaded");
            return this.CreateValidationError(ModelState);
        }

        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        var userId = _userManager.GetUserId(User);
        var imageUpload = await _fileService.UploadImage(file, userId);
        if (!imageUpload.Success)
        {
            return this.CreateAPIError(422, "logo-upload-failed", imageUpload.Response);
        }

        entity.EventLogo = imageUpload.StoredFile.Id;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == CurrentStoreId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(await ToEventData(entity, ticketsSold));
    }

    /// <summary>
    /// Remove the logo from an event.
    /// </summary>
    [HttpDelete("~/api/v1/stores/{storeId}/satoshi-tickets/events/{eventId}/logo")]
    public async Task<IActionResult> DeleteEventLogo(string storeId, string eventId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (entity == null)
            return EventNotFound();

        entity.EventLogo = null;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == CurrentStoreId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(await ToEventData(entity, ticketsSold));
    }

    private async Task<EventData> ToEventData(Event entity, int ticketsSold)
    {
        string eventLogoUrl = null;
        if (!string.IsNullOrEmpty(entity.EventLogo))
        {
            var fileUrl = await _fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventLogo);
            if (fileUrl != null)
                eventLogoUrl = await _uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(fileUrl));
        }

        return new EventData
        {
            Id = entity.Id,
            StoreId = entity.StoreId,
            Title = entity.Title,
            Description = entity.Description,
            EventType = entity.EventType.ToString(),
            Location = entity.Location,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Currency = entity.Currency,
            RedirectUrl = entity.RedirectUrl,
            EmailSubject = entity.EmailSubject,
            EmailBody = entity.EmailBody,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity,
            EventState = entity.EventState.ToString(),
            EventLogoFileId = entity.EventLogo,
            EventLogoUrl = eventLogoUrl,
            CreatedAt = entity.CreatedAt,
            TicketsSold = ticketsSold,
            PurchaseLink = Url.Action("EventSummary", "UITicketSalesPublic",
                new { storeId = entity.StoreId, eventId = entity.Id }, Request.Scheme)
        };
    }

    private IActionResult EventNotFound()
    {
        return this.CreateAPIError(404, "event-not-found", "The event was not found");
    }
}

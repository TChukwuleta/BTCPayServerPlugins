using System;
using System.Collections.Generic;
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
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;

[Route("~/api/v1/stores/{storeId}/satoshi-tickets/")]
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketsEventsController(StoreRepository storeRepo,
        IFileService fileService, UserManager<ApplicationUser> userManager, SimpleTicketSalesDbContextFactory dbContextFactory) : ControllerBase
{

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(string storeId, [FromQuery] bool expired = false)
    {
        await using var ctx = dbContextFactory.CreateContext();

        var eventsQuery = ctx.Events.Where(c => c.StoreId == storeId);
        if (expired)
            eventsQuery = eventsQuery.Where(e => e.StartDate <= DateTime.UtcNow);

        var events = eventsQuery.ToList();
        if (!events.Any())
            return Ok(Array.Empty<EventData>());

        var eventIds = events.Select(e => e.Id).ToHashSet();
        var ticketSoldByEvent = ctx.Tickets
            .Where(t => t.StoreId == storeId && eventIds.Contains(t.EventId) && t.PaymentStatus == TransactionStatus.Settled.ToString())
            .GroupBy(t => t.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.EventId, x => x.Count);

        /*var result = await Task.WhenAll(events.Select(e => ToEventData(e, ticketSoldByEvent.GetValueOrDefault(e.Id, 0))));*/
        var result = events.Select(e => ToEventData(e, ticketSoldByEvent.GetValueOrDefault(e.Id, 0))).ToArray();
        return Ok(result);
    }


    [HttpGet("events/{eventId}")]
    public async Task<IActionResult> GetEvent(string storeId, string eventId)
    {
        await using var ctx = dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == storeId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(ToEventData(entity, ticketsSold));
    }


    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromBody] ApiEventRequest request)
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

        EventType parsedEventType = default;
        if (string.IsNullOrEmpty(request.EventType) || !Enum.TryParse<EventType>(request.EventType, true, out parsedEventType))
            ModelState.AddModelError(nameof(request.EventType), "Invalid event type. Valid values: Virtual, Physical");

        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        var currency = request.Currency;
        if (string.IsNullOrWhiteSpace(currency))
        {
            var store = await storeRepo.FindStore(storeId);
            currency = store?.GetStoreBlob()?.DefaultCurrency ?? "USD";
        }
        var entity = new Event
        {
            StoreId = storeId,
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Currency = currency.Trim().ToUpperInvariant(),
            RedirectUrl = request.RedirectUrl,
            EmailSubject = request.EmailSubject,
            EmailBody = request.EmailBody,
            EventType = parsedEventType,
            HasMaximumCapacity = false,
            EventState = Data.EntityState.Disabled,
            CreatedAt = DateTime.UtcNow
        };
        await using var ctx = dbContextFactory.CreateContext();
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEvent), new { storeId, eventId = entity.Id }, ToEventData(entity, 0));
    }


    [HttpPut("events/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, [FromBody] ApiEventRequest request)
    {
        if (request == null)
        {
            ModelState.AddModelError(nameof(request), "Request body is required");
            return this.CreateValidationError(ModelState);
        }
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
            ModelState.AddModelError(nameof(request.Title), "Title is required");

        if (request.StartDate <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(request.StartDate), "Event date cannot be in the past");

        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
            ModelState.AddModelError(nameof(request.EndDate), "Event end date cannot be before start date");

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
        entity.HasMaximumCapacity = false;

        if (!string.IsNullOrEmpty(request.Currency))
            entity.Currency = request.Currency.Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(request.EventType))
            entity.EventType = Enum.Parse<EventType>(request.EventType, true);

        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets.Count(t => t.StoreId == storeId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());
        return Ok(ToEventData(entity, ticketsSold));
    }


    [HttpDelete("events/{eventId}")]
    public async Task<IActionResult> DeleteEvent(string storeId, string eventId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        var tickets = ctx.Tickets.Where(c => c.StoreId == storeId && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.StartDate > DateTime.UtcNow)
        {
            return this.CreateAPIError(422, "event-has-active-tickets", "Cannot delete event as there are active ticket purchases and the event is in the future");
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


    [HttpPut("events/{eventId}/toggle")]
    public async Task<IActionResult> ToggleEventStatus(string storeId, string eventId)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();
        if (!ticketTypes.Any() && entity.EventState == Data.EntityState.Disabled)
        {
            return this.CreateAPIError(422, "no-ticket-types",
                "Cannot activate event without ticket types. Create at least one ticket type first.");
        }
        entity.EventState = entity.EventState == Data.EntityState.Active ? Data.EntityState.Disabled : Data.EntityState.Active;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();

        var ticketsSold = ctx.Tickets
            .Count(t => t.StoreId == storeId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());

        return Ok(ToEventData(entity, ticketsSold));
    }


    [HttpPost("events/{eventId}/logo")]
    public async Task<IActionResult> UploadEventLogo(string storeId, string eventId, IFormFile file)
    {
        if (file == null)
        {
            ModelState.AddModelError(nameof(file), "No file was uploaded");
            return this.CreateValidationError(ModelState);
        }
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        var userId = userManager.GetUserId(User);
        var imageUpload = await fileService.UploadImage(file, userId);
        if (!imageUpload.Success)
            return this.CreateAPIError(422, "logo-upload-failed", imageUpload.Response);

        entity.EventLogo = imageUpload.StoredFile.Id;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();
        var ticketsSold = ctx.Tickets.Count(t => t.StoreId == storeId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());
        return Ok(ToEventData(entity, ticketsSold));
    }

    [HttpDelete("events/{eventId}/logo")]
    public async Task<IActionResult> DeleteEventLogo(string storeId, string eventId)
    {
        await using var ctx = dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        if (entity == null)
            return EventNotFound();

        if (!string.IsNullOrEmpty(entity.EventLogo))
        {
            var userId = userManager.GetUserId(User);
            await fileService.RemoveFile(entity.EventLogo, userId);
        }
        entity.EventLogo = null;
        ctx.Events.Update(entity);
        await ctx.SaveChangesAsync();
        var ticketsSold = ctx.Tickets.Count(t => t.StoreId == storeId && t.EventId == eventId && t.PaymentStatus == TransactionStatus.Settled.ToString());
        return Ok(ToEventData(entity, ticketsSold));
    }

    private EventData ToEventData(Event entity, int ticketsSold)
    {
        /*string eventLogoUrl = null;
        if (!string.IsNullOrEmpty(entity.EventLogo))
        {
            var fileUrl = await fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventLogo);
            if (fileUrl != null)
                eventLogoUrl = await uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(fileUrl));
        }*/

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
            TicketsSold = ticketsSold,
            RedirectUrl = entity.RedirectUrl,
            EmailSubject = entity.EmailSubject,
            EmailBody = entity.EmailBody,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity,
            EventState = entity.EventState.ToString(),
            CreatedAt = entity.CreatedAt,
            PurchaseLink = entity.EventState == Data.EntityState.Disabled ? null : Url.Action(nameof(UITicketSalesPublicController.EventSummary), "UITicketSalesPublic",
                new { storeId = entity.StoreId, eventId = entity.Id }, Request.Scheme)
        };
    }

    private IActionResult EventNotFound()
    {
        return this.CreateAPIError(404, "event-not-found", "The event was not found");
    }
}

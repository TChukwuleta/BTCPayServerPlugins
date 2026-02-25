using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Models.Api;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using EntityState = BTCPayServer.Plugins.SatoshiTickets.Data.EntityState;

namespace BTCPayServer.Plugins.SatoshiTickets.Controllers;


[Route("~/api/v1/stores/{storeId}/satoshi-tickets/")]
[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyStoreSettings)]
[EnableCors(CorsPolicies.All)]
public class GreenfieldSatoshiTicketTypesController : ControllerBase
{
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;

    public GreenfieldSatoshiTicketTypesController(SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private string CurrentStoreId => HttpContext.GetStoreData()?.Id;

    [HttpGet("events/{eventId}/ticket-types")]
    public async Task<IActionResult> GetTicketTypes(string storeId, string eventId,
        [FromQuery] string sortBy = "Name", [FromQuery] string sortDir = "asc")
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.Any(c => c.StoreId == CurrentStoreId && c.Id == eventId);
        if (!ticketEvent)
            return EventNotFound();

        var query = ctx.TicketTypes.Where(c => c.EventId == eventId);
        query = sortBy switch
        {
            "Price" => sortDir == "desc" ? query.OrderByDescending(t => t.Price) : query.OrderBy(t => t.Price),
            "Name" => sortDir == "desc" ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            _ => query.OrderBy(t => t.Name)
        };
        var list = query.ToList();
        var result = list.Select(ToTicketTypeData).ToArray();
        return Ok(result);
    }

    [HttpGet("events/{eventId}/ticket-types/{ticketTypeId}")]
    public async Task<IActionResult> GetTicketType(string storeId, string eventId, string ticketTypeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.Any(c => c.StoreId == CurrentStoreId && c.Id == eventId);
        if (!ticketEvent)
            return EventNotFound();

        var entity = ctx.TicketTypes.FirstOrDefault(c => c.EventId == eventId && c.Id == ticketTypeId);
        if (entity == null)
            return TicketTypeNotFound();

        return Ok(ToTicketTypeData(entity));
    }

    [HttpPost("events/{eventId}/ticket-types")]
    public async Task<IActionResult> CreateTicketType(string storeId, string eventId, [FromBody] CreateTicketTypeRequest request)
    {
        if (request == null)
        {
            ModelState.AddModelError(nameof(request), "Request body is required");
            return this.CreateValidationError(ModelState);
        }

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is required");

        if (request.Price <= 0)
            ModelState.AddModelError(nameof(request.Price), "Price cannot be zero or negative");

        if (request.Quantity <= 0 && ticketEvent.HasMaximumCapacity)
            ModelState.AddModelError(nameof(request.Quantity), "Quantity must be greater than zero");

        if (ticketEvent.HasMaximumCapacity)
        {
            var usedQuantity = await ctx.TicketTypes.Where(t => t.EventId == eventId).SumAsync(c => c.Quantity);
            if (request.Quantity > (ticketEvent.MaximumEventCapacity - usedQuantity))
                ModelState.AddModelError(nameof(request.Quantity),
                    "Quantity specified is higher than available event capacity");
        }

        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        var entity = new TicketType
        {
            EventId = eventId,
            Name = request.Name,
            Price = request.Price,
            Quantity = request.Quantity,
            Description = request.Description,
            IsDefault = request.IsDefault,
            TicketTypeState = EntityState.Active
        };
        var currentDefault = ctx.TicketTypes.FirstOrDefault(c => c.EventId == eventId && c.IsDefault);
        if (currentDefault == null)
        {
            entity.IsDefault = true;
        }
        else if (request.IsDefault)
        {
            currentDefault.IsDefault = false;
            entity.IsDefault = true;
        }
        else
        {
            entity.IsDefault = false;
        }

        ctx.TicketTypes.Add(entity);
        await ctx.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTicketType), new { storeId, eventId, ticketTypeId = entity.Id }, ToTicketTypeData(entity));
    }

    [HttpPut("events/{eventId}/ticket-types/{ticketTypeId}")]
    public async Task<IActionResult> UpdateTicketType(string storeId, string eventId, string ticketTypeId,
        [FromBody] UpdateTicketTypeRequest request)
    {
        if (request == null)
        {
            ModelState.AddModelError(nameof(request), "Request body is required");
            return this.CreateValidationError(ModelState);
        }

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStoreId);
        if (ticketEvent == null)
            return EventNotFound();

        var entity = ctx.TicketTypes.FirstOrDefault(c => c.Id == ticketTypeId && c.EventId == eventId);
        if (entity == null)
            return TicketTypeNotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is required");
        if (request.Price <= 0)
            ModelState.AddModelError(nameof(request.Price), "Price cannot be zero or negative");
        if (request.Quantity <= 0 && ticketEvent.HasMaximumCapacity)
            ModelState.AddModelError(nameof(request.Quantity), "Quantity must be greater than zero");
        if (ticketEvent.HasMaximumCapacity)
        {
            var usedQuantity = ctx.TicketTypes
                .Where(t => t.EventId == eventId && t.Id != ticketTypeId).Sum(c => c.Quantity);
            if (request.Quantity > (ticketEvent.MaximumEventCapacity - usedQuantity))
                ModelState.AddModelError(nameof(request.Quantity),
                    "Quantity specified is higher than available event capacity");
        }

        if (!ModelState.IsValid)
            return this.CreateValidationError(ModelState);

        entity.Name = request.Name;
        entity.Price = request.Price;
        entity.Quantity = request.Quantity;
        entity.Description = request.Description;

        if (request.IsDefault)
        {
            var currentDefault = ctx.TicketTypes.FirstOrDefault(t => t.EventId == eventId && t.Id != ticketTypeId && t.IsDefault);
            if (currentDefault != null)
            {
                currentDefault.IsDefault = false;
                entity.IsDefault = true;
            }
        }
        else
        {
            var anyDefault = ctx.TicketTypes.Any(t => t.EventId == eventId && t.Id != ticketTypeId && t.IsDefault);
            entity.IsDefault = !anyDefault;
        }
        await ctx.SaveChangesAsync();
        return Ok(ToTicketTypeData(entity));
    }


    [HttpDelete("ticket-types/{ticketTypeId}")]
    public async Task<IActionResult> DeleteTicketType(string storeId, string eventId, string ticketTypeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStoreId && c.Id == eventId);
        if (ticketEvent == null)
            return EventNotFound();

        var entity = ctx.TicketTypes.FirstOrDefault(c => c.EventId == eventId && c.Id == ticketTypeId);
        if (entity == null)
            return TicketTypeNotFound();

        ctx.TicketTypes.Remove(entity);

        // If we're deleting the default, reassign default to another ticket type
        if (entity.IsDefault)
        {
            var newDefault = ctx.TicketTypes.Where(t => t.EventId == eventId && t.Id != ticketTypeId)
                .OrderBy(t => t.Name).FirstOrDefault();
            if (newDefault != null)
                newDefault.IsDefault = true;
        }
        await ctx.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("ticket-types/{ticketTypeId}/toggle")]
    public async Task<IActionResult> ToggleTicketTypeStatus(string storeId, string eventId, string ticketTypeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStoreId && c.Id == eventId);
        if (ticketEvent == null)
            return EventNotFound();

        var entity = ctx.TicketTypes.FirstOrDefault(c => c.EventId == eventId && c.Id == ticketTypeId);
        if (entity == null)
            return TicketTypeNotFound();

        entity.TicketTypeState = entity.TicketTypeState == EntityState.Active ? EntityState.Disabled : EntityState.Active;
        await ctx.SaveChangesAsync();
        return Ok(ToTicketTypeData(entity));
    }

    private static TicketTypeData ToTicketTypeData(TicketType entity)
    {
        return new TicketTypeData
        {
            Id = entity.Id,
            EventId = entity.EventId,
            Name = entity.Name,
            Price = entity.Price,
            Description = entity.Description,
            Quantity = entity.Quantity,
            QuantitySold = entity.QuantitySold,
            QuantityAvailable = entity.Quantity - entity.QuantitySold,
            IsDefault = entity.IsDefault,
            TicketTypeState = entity.TicketTypeState.ToString()
        };
    }

    private IActionResult EventNotFound()
    {
        return this.CreateAPIError(404, "event-not-found", "The event was not found");
    }

    private IActionResult TicketTypeNotFound()
    {
        return this.CreateAPIError(404, "ticket-type-not-found", "The ticket type was not found");
    }
}

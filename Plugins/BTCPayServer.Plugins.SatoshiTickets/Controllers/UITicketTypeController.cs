using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Http;
using BTCPayServer.Abstractions.Models;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Abstractions.Constants;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using System;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/ticketevent/{eventId}/tickettype/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UITicketTypeController : Controller
{
    private readonly SimpleTicketSalesDbContextFactory _dbContextFactory;
    public UITicketTypeController(SimpleTicketSalesDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();


    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId, string eventId, string sortBy = "Name", string sortDir = "asc")
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null)
            return NotFound();

        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == ticketEvent.Id);
        ticketTypes = sortBy switch
        {
            "Price" => sortDir == "desc" ? ticketTypes.OrderByDescending(t => t.Price) : ticketTypes.OrderBy(t => t.Price),
            "Name" => sortDir == "desc" ? ticketTypes.OrderByDescending(t => t.Name) : ticketTypes.OrderBy(t => t.Name),
            _ => ticketTypes.OrderBy(t => t.Name)
        };
        var tickets = ticketTypes.ToList().Select(x =>
        {
            return new TicketTypeViewModel
            {
                TicketTypeId = x.Id,
                Name = x.Name,
                Price = x.Price,
                Quantity = x.Quantity,
                QuantitySold = x.QuantitySold,
                EventId = x.EventId,
                TicketTypeState = x.TicketTypeState,
                Description = x.Description,
                IsDefault = x.IsDefault,
            };
        }).ToList();
        return View(new TicketTypeListViewModel { SortBy = sortBy, SortDir = sortDir, TicketTypes = tickets, EventId = eventId });
    }


    [HttpGet("view")]
    public async Task<IActionResult> ViewTicketType(string storeId, string eventId, string ticketTypeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        var vm = new TicketTypeViewModel { EventId = eventId };
        if (!string.IsNullOrEmpty(ticketTypeId))
        {
            var entity = ctx.TicketTypes.FirstOrDefault(c => c.EventId == eventId && c.Id == ticketTypeId);
            if (entity == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Invalid event ticket type record specified";
                return RedirectToAction(nameof(List), new { storeId, eventId });
            }
            vm = TicketTypeToViewModel(entity);
        }
        vm.TicketHasMaximumCapacity = ticketEvent.HasMaximumCapacity;
        return View(vm);
    }


    [HttpPost("create")]
    public async Task<IActionResult> CreateTicketType(string storeId, string eventId, TicketTypeViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        if (vm.Price <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Price cannot be zero or negative";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        if (vm.Quantity <= 0 && ticketEvent.HasMaximumCapacity)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Price cannot be zero or negative";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        if (ticketEvent.HasMaximumCapacity && !ValidateTicketCapacity(ticketEvent, ctx.TicketTypes.Sum(c => c.Quantity), vm.Quantity))
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Quantity specified is higher than avaible event capacity ({ticketEvent.MaximumEventCapacity - ctx.TicketTypes.Sum(c => c.Quantity)}). Kindly update event to cater for more";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        var entity = TicketTypeViewModelToEntity(vm);
        entity.EventId = eventId;
        entity.TicketTypeState = EntityState.Active;

        var existingDefaultTicketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == entity.EventId && c.IsDefault);
        if (existingDefaultTicketType != null)
        {
            existingDefaultTicketType.IsDefault = vm.IsDefault ? false : true;
            ctx.TicketTypes.Update(existingDefaultTicketType);
        }
        else
            entity.IsDefault = true;

        ctx.TicketTypes.Add(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Ticket type created successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }


    [HttpPost("update/{ticketTypeId}")]
    public async Task<IActionResult> UpdateTicketType(string storeId, string eventId, string ticketTypeId, TicketTypeViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        var entity = ctx.TicketTypes.FirstOrDefault(c => c.Id == ticketTypeId && c.EventId == eventId);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid ticket type specifed";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        if (vm.Price <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Price cannot be zero or negative";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        if (vm.Quantity <= 0 && ticketEvent.HasMaximumCapacity)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Price cannot be zero or negative";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        if (ticketEvent.HasMaximumCapacity && !ValidateTicketCapacity(ticketEvent, ctx.TicketTypes.Sum(c => c.Quantity), vm.Quantity))
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Quantity specified is higher than avaible event capacity ({ticketEvent.MaximumEventCapacity - ctx.TicketTypes.Sum(c => c.Quantity)}). Kindly update event to cater for more";
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        entity.Name = vm.Name;
        entity.Price = vm.Price;
        entity.Quantity = vm.Quantity;
        entity.Description = vm.Description;
        entity.IsDefault = vm.IsDefault;

        if (!entity.IsDefault)
        {
            var existingDefaultTicketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == entity.EventId && c.IsDefault);
            if (existingDefaultTicketType == null)
                entity.IsDefault = true;
        }
        ctx.TicketTypes.Update(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Ticket type updated successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }


    [HttpGet("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleTicketTypeStatus(string storeId, string eventId, string ticketTypeId, bool enable)
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
        return View("Confirm", new ConfirmModel($"{(enable ? "Activate" : "Disable")} ticket type", $"The ticket type ({ticketType.Name}) will be {(enable ? "activated" : "disabled")}. Are you sure?", (enable ? "Activate" : "Disable")));
    }


    [HttpPost("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleTicketTypeStatusPost(string storeId, string eventId, string ticketTypeId, bool enable)
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
            case EntityState.Disabled:
                ticketType.TicketTypeState = EntityState.Active;
                break;
            case EntityState.Active:
            default:
                ticketType.TicketTypeState = EntityState.Disabled;
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
        return View("Confirm", new ConfirmModel($"Delete Ticket Type", $"Ticket type: {ticketType.Name} would also be deleted. Are you sure?", $"Delete {ticketType.Name}"));
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

    private bool ValidateTicketCapacity(Event ticketEvent, int quantityOfTicketsUsed, int ticketModelQuantity)
        => ticketModelQuantity <= (ticketEvent.MaximumEventCapacity - quantityOfTicketsUsed);


    private TicketTypeViewModel TicketTypeToViewModel(TicketType entity)
    {
        return new TicketTypeViewModel
        {
            EventId = entity.EventId,
            TicketTypeId = entity.Id,
            Name = entity.Name,
            Price = entity.Price,
            Quantity = entity.Quantity,
            QuantitySold = entity.QuantitySold,
            TicketTypeState = entity.TicketTypeState,
            Description = entity.Description
        };
    }

    private TicketType TicketTypeViewModelToEntity(TicketTypeViewModel model)
    {
        return new TicketType
        {
            Name = model.Name,
            Price = model.Price,
            EventId = model.EventId,
            Quantity = model.Quantity,
            QuantitySold = model.QuantitySold,
            TicketTypeState = model.TicketTypeState,
            Description = model.Description,
            IsDefault = model.IsDefault
        };
    }
}

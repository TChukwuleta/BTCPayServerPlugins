using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using EntityState = BTCPayServer.Plugins.SatoshiTickets.Data.EntityState;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/satoshi-tickets/event/{eventId}/tickettype/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[AutoValidateAntiforgeryToken]
public class UITicketTypeController(SimpleTicketSalesDbContextFactory dbContextFactory) : Controller
{
    private StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId, string eventId, string sortBy = "Name", string sortDir = "asc")
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

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
                StoreId = CurrentStore.Id,
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
        return View(new TicketTypeListViewModel { SortBy = sortBy, SortDir = sortDir, TicketTypes = tickets, EventId = eventId, StoreId = CurrentStore.Id });
    }


    [HttpGet("view")]
    public async Task<IActionResult> ViewTicketType(string storeId, string eventId, string ticketTypeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        var vm = new TicketTypeViewModel { EventId = eventId, StoreId = CurrentStore.Id };
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

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        if (!ValidateTicketType(ctx, ticketEvent, vm, null, out string errorMessage))
        {
            TempData[WellKnownTempData.ErrorMessage] = errorMessage;
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        var entity = TicketTypeViewModelToEntity(vm, eventId);
        var currentDefault = ctx.TicketTypes.FirstOrDefault(c => c.EventId == entity.EventId && c.IsDefault);
        if (currentDefault is not null && entity.IsDefault)
            currentDefault.IsDefault = false;
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

        await using var ctx = dbContextFactory.CreateContext();
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
        var validateTicketType = ValidateTicketType(ctx, ticketEvent, vm, ticketTypeId, out string errorMessage);
        if (!validateTicketType)
        {
            TempData[WellKnownTempData.ErrorMessage] = errorMessage;
            return RedirectToAction(nameof(ViewTicketType), new { storeId, eventId });
        }
        entity.Name = vm.Name;
        entity.Price = vm.Price;
        entity.Quantity = vm.Quantity;
        entity.Description = vm.Description;
        entity.IsDefault = vm.IsDefault;
        if (!entity.IsDefault)
        {
            var anyDefault = ctx.TicketTypes.Any(t => t.EventId == eventId && t.Id != ticketTypeId && t.IsDefault);
            if (!anyDefault) entity.IsDefault = true;
        }
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Ticket type updated successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }

    private bool ValidateTicketType(SimpleTicketSalesDbContext ctx, Event ticketEvent, TicketTypeViewModel vm, string? excludeTicketTypeId, out string error)
    {
        error = string.Empty;
        if (vm.Price <= 0)
        {
            error = "Price cannot be zero or negative";
            return false;
        }
        if (vm.Quantity <= 0 && ticketEvent.HasMaximumCapacity)
        {
            error = "Quantity must be greater than zero";
            return false;
        }
        if (ticketEvent.HasMaximumCapacity && !ValidateTicketCapacity(ticketEvent, ctx.TicketTypes.Where(t => t.EventId == ticketEvent.Id && t.Id != excludeTicketTypeId).Sum(c => c.Quantity), vm.Quantity))
        {
            error = $"Quantity specified is higher than available event capacity. Kindly update event to cater for more";
            return false;
        }
        return true;
    }


    [HttpGet("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleTicketTypeStatus(string storeId, string eventId, string ticketTypeId, bool enable)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }

        var action = enable ? "Activate" : "Disable";
        return View("Confirm",
            new ConfirmModel($"{action} ticket type", $"The ticket type ({ticketType.Name}) will be {(enable ? "activated" : "disabled")}. Are you sure?", action));
    }


    [HttpPost("toggle/{ticketTypeId}")]
    public async Task<IActionResult> ToggleTicketTypeStatusPost(string storeId, string eventId, string ticketTypeId, bool enable)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

        var ticketType = ctx.TicketTypes.FirstOrDefault(c => c.EventId == ticketEvent.Id && c.Id == ticketTypeId);
        if (ticketType == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid route specified";
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }
        ticketType.TicketTypeState = enable ? EntityState.Active : EntityState.Disabled;
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Ticket type {(enable ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(List), new { storeId, eventId });
    }


    [HttpGet("delete/{ticketTypeId}")]
    public async Task<IActionResult> DeleteTicketType(string storeId, string eventId, string ticketTypeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

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
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == eventId);
        if (ticketEvent == null) return NotFound();

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

    private bool ValidateTicketCapacity(Event ticketEvent, int quantityOfTicketsUsed, int ticketModelQuantity) => ticketModelQuantity <= (ticketEvent.MaximumEventCapacity - quantityOfTicketsUsed);

    private TicketTypeViewModel TicketTypeToViewModel(TicketType entity)
    {
        return new TicketTypeViewModel
        {
            StoreId = CurrentStore.Id,
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

    private TicketType TicketTypeViewModelToEntity(TicketTypeViewModel model, string eventId)
    {
        return new TicketType
        {
            Name = model.Name,
            Price = model.Price,
            EventId = eventId,
            Quantity = model.Quantity,
            QuantitySold = model.QuantitySold,
            TicketTypeState = EntityState.Active,
            Description = model.Description,
            IsDefault = model.IsDefault
        };
    }
}

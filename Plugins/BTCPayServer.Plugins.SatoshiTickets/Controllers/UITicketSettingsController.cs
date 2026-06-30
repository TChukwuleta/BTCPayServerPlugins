using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/satoshi-tickets/event/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[AutoValidateAntiforgeryToken]
public class UITicketSettingsController(EmailService emailService, SimpleTicketSalesDbContextFactory dbContextFactory) : Controller
{
    private StoreData CurrentStore => HttpContext.GetStoreData();


    [HttpGet("{eventId}/discount-codes")]
    public async Task<IActionResult> DiscountCodes(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);    
        if (ticketEvent == null) return NotFound();

        var ticketTypeNames = ctx.TicketTypes.Where(c => c.EventId == eventId).ToDictionary(c => c.Id, c => c.Name);
        var codes = ctx.DiscountCodes.Where(c => c.EventId == eventId && c.StoreId == CurrentStore.Id).OrderByDescending(c => c.CreatedAt).ToList();

        var vm = new DiscountCodeListViewModel
        {
            StoreId = CurrentStore.Id,
            EventId = eventId,
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency ?? CurrentStore.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant(),
            Codes = codes.Select(c => new DiscountCodeListItemViewModel
            {
                Id = c.Id,
                Code = c.Code,
                DiscountType = c.DiscountType,
                Value = c.Value,
                MaxUses = c.MaxUses,
                UsesCount = c.UsesCount,
                ExpiryDate = c.ExpiryDate,
                DiscountCodeState = c.DiscountCodeState,
                TicketTypeName = c.TicketTypeId != null ? ticketTypeNames.GetValueOrDefault(c.TicketTypeId) : null
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet("{eventId}/discount-codes/create")]
    public async Task<IActionResult> CreateDiscountCode(string storeId, string eventId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null) return NotFound();

        var vm = new UpsertDiscountCodeViewModel
        {
            StoreId = CurrentStore.Id,
            EventId = eventId,
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency ?? CurrentStore.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant(),
            TicketTypeOptions = GetTicketTypeOptions(ctx, eventId)
        };
        return View("UpsertDiscountCode", vm);
    }

    [HttpGet("{eventId}/discount-codes/{discountCodeId}/edit")]
    public async Task<IActionResult> EditDiscountCode(string storeId, string eventId, string discountCodeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null) return NotFound();

        var discountCode = ctx.DiscountCodes.FirstOrDefault(c => c.Id == discountCodeId && c.EventId == eventId && c.StoreId == CurrentStore.Id);
        if (discountCode == null) return NotFound();

        var vm = new UpsertDiscountCodeViewModel
        {
            Id = discountCode.Id,
            StoreId = CurrentStore.Id,
            EventId = eventId,
            EventTitle = ticketEvent.Title,
            Currency = ticketEvent.Currency ?? CurrentStore.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant(),
            Code = discountCode.Code,
            DiscountType = discountCode.DiscountType,
            Value = discountCode.Value,
            TicketTypeId = discountCode.TicketTypeId,
            MaxUses = discountCode.MaxUses,
            ExpiryDate = discountCode.ExpiryDate,
            IsActive = discountCode.DiscountCodeState == DiscountCodeState.Active,
            TicketTypeOptions = GetTicketTypeOptions(ctx, eventId)
        };
        return View("UpsertDiscountCode", vm);
    }


    [HttpPost("{eventId}/discount-codes/save")]
    public async Task<IActionResult> SaveDiscountCode(string storeId, string eventId, UpsertDiscountCodeViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == CurrentStore.Id);
        if (ticketEvent == null) return NotFound();

        var normalizedCode = DiscountCodeService.Normalize(vm.Code);
        if (string.IsNullOrEmpty(normalizedCode))
            ModelState.AddModelError(nameof(vm.Code), "A discount code is required.");

        if (vm.DiscountType == DiscountType.Percentage && (vm.Value <= 0 || vm.Value >= 100))
            ModelState.AddModelError(nameof(vm.Value), "Percentage discounts must be between 1 and 99. Use the guest list for free tickets.");

        if (vm.DiscountType == DiscountType.FixedAmount && vm.Value <= 0)
            ModelState.AddModelError(nameof(vm.Value), "A fixed discount must be greater than zero.");

        var codeClash = ctx.DiscountCodes.Any(d => d.EventId == eventId && d.Code == normalizedCode && d.Id != vm.Id);
        if (codeClash)
            ModelState.AddModelError(nameof(vm.Code), "A discount code with this name already exists for this event.");

        if (!ModelState.IsValid)
        {
            vm.StoreId = CurrentStore.Id;
            vm.EventId = eventId;
            vm.EventTitle = ticketEvent.Title;
            vm.Currency = ticketEvent.Currency;
            vm.TicketTypeOptions = GetTicketTypeOptions(ctx, eventId);
            return View("UpsertDiscountCode", vm);
        }
        var ticketTypeId = string.IsNullOrEmpty(vm.TicketTypeId) ? null : vm.TicketTypeId;
        DateTimeOffset? expiry = vm.ExpiryDate;
        if (string.IsNullOrEmpty(vm.Id))
        {
            ctx.DiscountCodes.Add(new DiscountCode
            {
                StoreId = CurrentStore.Id,
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                Code = normalizedCode,
                DiscountType = vm.DiscountType,
                Value = vm.Value,
                MaxUses = vm.MaxUses,
                MinQuantity = vm.MinQuantity,
                ExpiryDate = expiry,
                UsesCount = 0,
                DiscountCodeState = vm.IsActive ? DiscountCodeState.Active : DiscountCodeState.Disabled,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            var existingCode = ctx.DiscountCodes.FirstOrDefault(d => d.Id == vm.Id && d.EventId == eventId && d.StoreId == CurrentStore.Id);
            if (existingCode == null)
                return NotFound();

            existingCode.Code = normalizedCode;
            existingCode.DiscountType = vm.DiscountType;
            existingCode.Value = vm.Value;
            existingCode.TicketTypeId = ticketTypeId;
            existingCode.MaxUses = vm.MaxUses;
            existingCode.MinQuantity = vm.MinQuantity;
            existingCode.ExpiryDate = expiry;
            existingCode.DiscountCodeState = vm.IsActive ? DiscountCodeState.Active : DiscountCodeState.Disabled;
        }
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Discount code saved successfully";
        return RedirectToAction(nameof(DiscountCodes), new { storeId, eventId });
    }


    [HttpGet("{eventId}/discount-codes/{discountCodeId}/toggle")]
    public async Task<IActionResult> ToggleDiscountCode(string storeId, string eventId, string discountCodeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var code = ctx.DiscountCodes.FirstOrDefault(d => d.StoreId == storeId && d.EventId == eventId && d.Id == discountCodeId);
        if(code == null) return NotFound();

        code.DiscountCodeState = code.DiscountCodeState == DiscountCodeState.Active ? DiscountCodeState.Disabled : DiscountCodeState.Active;
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Discount code {(code.DiscountCodeState == DiscountCodeState.Active ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(DiscountCodes), new { storeId, eventId });
    }


    [HttpGet("{eventId}/discount-codes/{discountCodeId}/delete")]
    public async Task<IActionResult> DeleteDiscountCode(string storeId, string eventId, string discountCodeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var code = ctx.DiscountCodes.FirstOrDefault(d => d.StoreId == storeId && d.EventId == eventId && d.Id == discountCodeId);
        if (code == null) return NotFound();

        if (code.UsesCount > 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot delete discount code as it has been used";
            return RedirectToAction(nameof(DiscountCodes), new { storeId, eventId });
        }
        ctx.DiscountCodes.Remove(code);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Discount code deleted";
        return RedirectToAction(nameof(DiscountCodes), new { storeId, eventId });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var settings = ctx.SatoshiTicketsSettings.FirstOrDefault(s => s.StoreId == CurrentStore.Id);
        ViewData["StoreEmailSettingsConfigured"] = await emailService.IsEmailSettingsConfigured(CurrentStore.Id);
        var vm = new SatoshiTicketsSettingsViewModel
        {
            StoreId = CurrentStore.Id,
            EnableAutoReminders = settings?.EnableAutoReminders ?? false,
            DefaultReminderDaysBeforeEvent = settings?.DefaultReminderDaysBeforeEvent ?? 3,
            ReminderEmailBody = settings?.ReminderEmailBody,
            ReminderEmailSubject = settings?.ReminderEmailSubject
        };
        return View(vm);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(string storeId, SatoshiTicketsSettingsViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        if (vm.EnableAutoReminders && vm.DefaultReminderDaysBeforeEvent <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Default reminder days must be greater than 0";
            return RedirectToAction(nameof(Settings), new { storeId });
        }
        await using var ctx = dbContextFactory.CreateContext();
        var settings = ctx.SatoshiTicketsSettings.FirstOrDefault(s => s.StoreId == CurrentStore.Id);
        if (settings == null)
        {
            ctx.SatoshiTicketsSettings.Add(new SatoshiTicketsSetting
            {
                StoreId = CurrentStore.Id,
                EnableAutoReminders = vm.EnableAutoReminders,
                DefaultReminderDaysBeforeEvent = vm.DefaultReminderDaysBeforeEvent,
                ReminderEmailSubject = vm.ReminderEmailSubject,
                ReminderEmailBody = vm.ReminderEmailBody
            });
        }
        else
        {
            settings.EnableAutoReminders = vm.EnableAutoReminders;
            settings.DefaultReminderDaysBeforeEvent = vm.DefaultReminderDaysBeforeEvent;
            settings.ReminderEmailBody = vm.ReminderEmailBody;
            settings.ReminderEmailSubject = vm.ReminderEmailSubject;
        }
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Reminder settings updated successfully";
        return RedirectToAction(nameof(Settings), new { storeId });
    }

    private static List<DiscountTicketTypeOption> GetTicketTypeOptions(SimpleTicketSalesDbContext ctx, string eventId)
    {
        return ctx.TicketTypes.Where(c => c.EventId == eventId).OrderBy(t => t.Name)
            .Select(c => new DiscountTicketTypeOption
            {
                Id = c.Id,
                Name = c.Name
            }).ToList();
    }
}

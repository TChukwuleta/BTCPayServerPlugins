using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Emails;
using BTCPayServer.Plugins.Emails.Controllers;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels.Models;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.SatoshiTickets;


[Route("~/plugins/{storeId}/satoshi-tickets/event/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
[AutoValidateAntiforgeryToken]
public class UITicketSalesController(UriResolver uriResolver,
        IFileService fileService,
        StoreRepository storeRepo,
        EmailService emailService,
        TicketService ticketService,
        SimpleTicketSalesDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager) : Controller
{

    private async Task<StoreData?> GetStoreData(string id) => await storeRepo.FindStore(id);


    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId, bool expired)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var query = ctx.Events.Where(e => e.StoreId == store.Id).Select(e => new
        {
            e.Id,
            e.Location,
            e.Title,
            e.Description,
            e.StartDate,
            e.CreatedAt,
            e.EventState,
            TicketCount = ctx.Tickets.Count(t => t.EventId == e.Id && t.StoreId == store.Id && t.PaymentStatus == TransactionStatus.Settled.ToString())
        });

        if (expired)
            query = query.Where(e => e.StartDate <= DateTime.UtcNow);

        var eventsData = query.ToList();
        var eventsViewModel = eventsData.Select(e => new SalesTicketsEventsListViewModel
        {
            Id = e.Id,
            Location = e.Location,
            Title = e.Title,
            Description = e.Description,
            EventDate = e.StartDate,
            CreatedAt = e.CreatedAt,
            StoreId = store.Id,
            EventState = e.EventState,
            TicketSold = e.TicketCount,
            EventPurchaseLink = Url.Action(nameof(UITicketSalesPublicController.EventSummary), "UITicketSalesPublic", new { storeId, eventId = e.Id }, Request.Scheme)
        }).ToList();

        var isEmailSettingsConfigured = await emailService.IsEmailSettingsConfigured(store.Id);
        ViewData["StoreEmailSettingsConfigured"] = isEmailSettingsConfigured;
        if (!isEmailSettingsConfigured)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"Kindly <a href='{Url.Action(action: nameof(UIStoresEmailController.StoreEmailSettings), controller: "UIStoresEmail",
                    values: new
                    {
                        area = EmailsPlugin.Area,
                        storeId = store.Id
                    })}' class='alert-link'>configure Email SMTP</a> to create an event",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
        }
        var vm = new SalesTicketsEventsViewModel { DisplayedEvents = eventsViewModel, Expired = expired, StoreId = store.Id };
        return View(vm);
    }


    [HttpGet("view")]
    public async Task<IActionResult> ViewEvent(string storeId, string eventId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();

        var vm = new UpdateSimpleTicketSalesEventViewModel { StoreId = store.Id, StoreDefaultCurrency = store.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant() };
        if (!string.IsNullOrEmpty(eventId))
        {
            var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
            if (entity == null)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Invalid event record specified for this store";
                return RedirectToAction(nameof(List), new { storeId });
            }
            vm = TicketSalesEventToViewModel(entity);
            var getFile = entity.EventLogo == null ? null : await fileService.GetFileUrl(Request.GetAbsoluteRootUri(), entity.EventLogo);
            vm.EventImageUrl = getFile == null ? null : await uriResolver.Resolve(Request.GetAbsoluteRootUri(), new UnresolvedUri.Raw(getFile));
            vm.StoreDefaultCurrency = entity.Currency ?? store.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant();
        }
        vm.EventTypes = Enum.GetValues(typeof(EventType)).Cast<EventType>()
            .Select(e => new SelectListItem
            {
                Value = e.ToString(),
                Text = e.ToString()
            }).ToList();
        return View(vm);
    }


    [HttpPost("create")]
    public async Task<IActionResult> CreateEvent(string storeId, [FromForm] UpdateSimpleTicketSalesEventViewModel vm)
    {
        if (vm.StartDate <= DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event date cannot be in the past";
            return RedirectToAction(nameof(ViewEvent), new { storeId });
        }
        if (vm.EndDate.HasValue && vm.EndDate.Value < vm.StartDate)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event end date cannot be before start date";
            return RedirectToAction(nameof(ViewEvent), new { storeId });
        }
        if (vm.HasMaximumCapacity && (!vm.MaximumEventCapacity.HasValue || vm.MaximumEventCapacity.Value <= 0))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Kindly input the event capacity";
            return RedirectToAction(nameof(ViewEvent), new { storeId });
        }

        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();

        var entity = TicketSalesEventViewModelToEntity(vm, null, store.Id);
        entity.EventState = Data.EntityState.Disabled;
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await fileService.UploadImage(vm.EventImageFile, GetUserId());
            if (!imageUpload.Success)
            {
                TempData[WellKnownTempData.ErrorMessage] = imageUpload.Response;
                return RedirectToAction(nameof(ViewEvent), new { storeId = store.Id });
            }
            else
            {
                entity.EventLogo = imageUpload.StoredFile.Id;
            }
        }
        entity.CreatedAt = DateTime.UtcNow;
        entity.Currency = vm.Currency ?? store.GetStoreBlob().DefaultCurrency.Trim().ToUpperInvariant();
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event created successfully. Kindly create ticket tiers for your event to publish your event";
        return RedirectToAction(nameof(UITicketTypeController.List), "UITicketType", new { storeId, eventId = entity.Id });
    }


    [HttpPost("update/{eventId}")]
    public async Task<IActionResult> UpdateEvent(string storeId, string eventId, UpdateSimpleTicketSalesEventViewModel vm, [FromForm] bool RemoveEventLogoFile = false)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid event record specified for this store";
            return RedirectToAction(nameof(List), new { storeId });
        }
        var ticketTiersCount = ctx.TicketTypes.Where(t => t.EventId == eventId).Sum(c => c.Quantity);
        if (vm.HasMaximumCapacity && vm.MaximumEventCapacity < ticketTiersCount)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Maximum capacity is less that the sum of all tiers capacity. Kindly increase capacity";
            return RedirectToAction(nameof(ViewEvent), new { storeId, eventId });
        }
        if (vm.EndDate is DateTime endDate && endDate < vm.StartDate)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Event end date cannot be before start date";
            return RedirectToAction(nameof(ViewEvent), new { storeId, eventId });
        }
        entity = TicketSalesEventViewModelToEntity(vm, entity, store.Id);
        UploadImageResultModel imageUpload = null;
        if (vm.EventImageFile != null)
        {
            imageUpload = await fileService.UploadImage(vm.EventImageFile, GetUserId());
            if (!imageUpload.Success)
            {
                TempData[WellKnownTempData.ErrorMessage] = imageUpload.Response;
                return RedirectToAction(nameof(ViewEvent), new { storeId, eventId });
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
        }
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event updated successfully";
        return RedirectToAction(nameof(List), new { storeId });
    }

    [HttpGet("toggle/{eventId}")]
    public async Task<IActionResult> ToggleTicketEventStatus(string storeId, string eventId, bool enable)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var hasTicketTypes = ctx.TicketTypes.Any(c => c.EventId == eventId);
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == store.Id && c.Id == eventId);
        if (ticketEvent == null || !hasTicketTypes)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"No ticket tier available. Click <a href='{Url.Action(nameof(UITicketTypeController.List), "UITicketType", new { storeId, eventId })}' class='alert-link'>here</a> to create one",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }

        var action = enable ? "Activate" : "Disable";
        return View("Confirm",
            new ConfirmModel( $"{action} event", $"The event ({ticketEvent.Title}) will be {(enable ? "activated" : "disabled")}. Are you sure?", action));
    }


    [HttpPost("toggle/{eventId}")]
    public async Task<IActionResult> ToggleTicketEventStatusPost(string storeId, string eventId, bool enable)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var hasTicketTypes = ctx.TicketTypes.Any(c => c.EventId == eventId);
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.StoreId == store.Id && c.Id == eventId);
        if (ticketEvent == null || !hasTicketTypes)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"No ticket type available. Click <a href='{Url.Action(nameof(UITicketTypeController.List), "UITicketType", new { storeId, eventId })}' class='alert-link'>here</a> to create one",
                Severity = StatusMessageModel.StatusSeverity.Info
            });
            return RedirectToAction(nameof(List), new { storeId, eventId });
        }

        ticketEvent.EventState = ticketEvent.EventState == Data.EntityState.Active ? Data.EntityState.Disabled : Data.EntityState.Active;
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Event {(enable ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(List), new { storeId });
    }

    [HttpGet("delete/{eventId}")]
    public async Task<IActionResult> DeleteEvent(string storeId, string eventId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
        if (entity == null)
            return NotFound();

        var hasTickets = ctx.Tickets.Any(c => c.StoreId == store.Id && c.EventId == eventId);
        if (hasTickets && entity.StartDate > DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchase and the event is in the future";
            return RedirectToAction(nameof(List), new { storeId });
        }
        return View("Confirm", new ConfirmModel($"Delete Event", $"All tickets associated with this Event: {entity.Title} would also be deleted. Are you sure?", "Delete Event"));
    }


    [HttpPost("delete/{eventId}")]
    public async Task<IActionResult> DeleteEventPost(string storeId, string eventId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();

        var entity = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
        if (entity == null)
            return NotFound();

        var tickets = ctx.Tickets.Where(c => c.StoreId == store.Id && c.EventId == eventId).ToList();
        if (tickets.Any() && entity.StartDate > DateTime.UtcNow)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot delete event as there are active tickets purchased and the event is in the future";
            return RedirectToAction(nameof(List), new { storeId });
        }
        var ticketTypes = ctx.TicketTypes.Where(c => c.EventId == eventId).ToList();    
        if (tickets.Any())
        {
            ctx.Tickets.RemoveRange(tickets);
        }
        if (ticketTypes.Any())
        {
            ctx.TicketTypes.RemoveRange(ticketTypes);
        }
        ctx.Events.Remove(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Event deleted successfully";
        return RedirectToAction(nameof(List), new { storeId });
    }


    [HttpGet("{eventId}/tickets")]
    public async Task<IActionResult> ViewEventTicket(string storeId, string eventId, string searchText)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();

        var entity = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
        if (entity == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Invalid Event specified";
            return RedirectToAction(nameof(List), new { storeId });
        }

        var ordersQuery = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .Where(c => c.EventId == eventId && c.StoreId == store.Id && c.PaymentStatus == TransactionStatus.Settled.ToString());

        var settledTickets = ordersQuery.SelectMany(o => o.Tickets).Where(t => t.PaymentStatus == TransactionStatus.Settled.ToString()).ToList();
        int ticketsBought = settledTickets.Count();
        int ticketsChecked = settledTickets.Count(c => c.UsedAt.HasValue);

        if (!string.IsNullOrEmpty(searchText))
        {
            ordersQuery = ordersQuery.Where(o =>
                o.InvoiceId.Contains(searchText) ||
                o.Tickets.Any(t =>
                    t.TxnNumber.Contains(searchText) || t.FirstName.Contains(searchText) ||
                    t.LastName.Contains(searchText) || t.Email.Contains(searchText)));
        }
        var orders = ordersQuery.ToList();

        var vm = new EventTicketViewModel
        {
            TicketsCount = ticketsBought,
            CheckedInTicketsCount = ticketsChecked,
            StoreId = storeId,
            EventId = eventId,
            EventTitle = entity.Title,
            SearchText = searchText,
            TicketOrders = orders.Select(o =>
            {
                var settledOrderTickets = o.Tickets.Where(t => t.PaymentStatus == TransactionStatus.Settled.ToString()).ToList();
                var firstTicket = settledOrderTickets.FirstOrDefault();
                return new EventTicketOrdersVm
                {
                    OrderId = o.Id,
                    Quantity = settledOrderTickets.Count,
                    InvoiceId = o.InvoiceId,
                    HasEmailNotificationBeenSent = o.EmailSent,
                    FirstName = firstTicket?.FirstName,
                    LastName = firstTicket?.LastName,
                    Email = firstTicket?.Email,
                    PurchaseDate = o.PurchaseDate!.Value,
                    Tickets = settledOrderTickets.Select(t => new EventContactPersonTicketVm
                    {
                        Id = t.Id,
                        Amount = t.Amount,
                        TicketTypeId = t.TicketTypeId,
                        FirstName = t.FirstName,
                        LastName = t.LastName,
                        Email = t.Email,
                        TicketNumber = t.TxnNumber,
                        Currency = o.Currency,
                        CheckedIn = t.UsedAt.HasValue,
                        TicketTypeName = t.TicketTypeName
                    }).ToList()
                };
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet("{eventId}/tickets/{ticketNumber}/check-in")]
    public async Task<IActionResult> CheckinTicketAttendee(string storeId, string eventId, string ticketNumber)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        var checkinTicket = await ticketService.CheckinTicket(eventId, ticketNumber, store.Id);
        if (checkinTicket.Success)
        {
            TempData[WellKnownTempData.SuccessMessage] = $"Ticket for {checkinTicket.Ticket.FirstName} {checkinTicket.Ticket.LastName} of ticket type: {checkinTicket.Ticket.TicketTypeName} checked-in successfully";
        }
        else
        {
            TempData[WellKnownTempData.ErrorMessage] = checkinTicket.ErrorMessage;
        }
        return RedirectToAction(nameof(ViewEventTicket), new { storeId, eventId });
    }

    [HttpGet("{eventId}/send-reminder/{orderId}/{ticketId}")]
    public async Task<IActionResult> SendTicketReminder(string storeId, string eventId, string orderId, string ticketId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == eventId && c.StoreId == store.Id);
        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .FirstOrDefault(o => o.Id == orderId && o.StoreId == store.Id && o.EventId == eventId && o.Tickets.Any());

        if (ticketEvent == null || order == null || !order.Tickets.Any())
            return NotFound();

        return View(new SendTicketReminderViewModel
        {
            TicketId = ticketId,
            EventId = eventId,
            Email = order.Tickets.First(a => a.Id == ticketId)?.Email,
            OrderId = orderId,
        });
    }

    [HttpPost("{eventId}/send-reminder/{orderId}/{ticketId}")]
    public async Task<IActionResult> SendTicketReminder(string storeId, SendTicketReminderViewModel model)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.AsNoTracking().FirstOrDefault(c => c.Id == model.EventId && c.StoreId == store.Id);

        var order = ctx.Orders.AsNoTracking().Include(c => c.Tickets)
            .FirstOrDefault(o => o.Id == model.OrderId && o.StoreId == store.Id && o.EventId == model.EventId);

        if (ticketEvent == null || order == null)
            return NotFound();

        var ticket = order.Tickets.FirstOrDefault(c => c.Id == model.TicketId);
        if (ticket == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Invalid Ticket specified";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = store.Id, eventId = model.EventId });
        }
        if (!await emailService.IsEmailSettingsConfigured(store.Id))
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Email settings not setup. Kindly configure Email SMTP in the admin settings";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = store.Id, eventId = model.EventId });
        }
        try
        {
            var emailResponse = await emailService.SendTicketRegistrationEmail(store.Id, order.Tickets.First(), ticketEvent);
            if (emailResponse.IsSuccessful)
                order.EmailSent = true;

            ctx.Orders.Update(order);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured when sending ticket details. {ex.Message}";
            return RedirectToAction(nameof(ViewEventTicket), new { storeId = store.Id, eventId = model.EventId });
        }
        TempData[WellKnownTempData.SuccessMessage] = $"Ticket details has been sent to recipients via email";
        return RedirectToAction(nameof(ViewEventTicket), new { storeId = store.Id, eventId = model.EventId });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(string storeId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var settings = ctx.SatoshiTicketsSettings.FirstOrDefault(s => s.StoreId == store.Id);

        ViewData["StoreEmailSettingsConfigured"] = await emailService.IsEmailSettingsConfigured(store.Id);
        var vm = new SatoshiTicketsSettingsViewModel
        {
            StoreId = store.Id,
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
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        if (vm.EnableAutoReminders && vm.DefaultReminderDaysBeforeEvent <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Default reminder days must be greater than 0";
            return RedirectToAction(nameof(Settings), new { storeId });
        }

        await using var ctx = dbContextFactory.CreateContext();
        var settings = ctx.SatoshiTicketsSettings.FirstOrDefault(s => s.StoreId == store.Id);
        if (settings == null)
        {
            ctx.SatoshiTicketsSettings.Add(new SatoshiTicketsSetting
            {
                StoreId = store.Id,
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

    [HttpGet("{eventId}/export")]
    public async Task<IActionResult> ExportTickets(string storeId, string eventId)
    {
        if (await GetStoreData(storeId) is not { } store)
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var ticketEvent = ctx.Events.FirstOrDefault(c => c.Id == eventId && c.StoreId == storeId);
        var ordersWithTickets = ctx.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.EventId == eventId && o.PaymentStatus == TransactionStatus.Settled.ToString())
            .SelectMany(o => o.Tickets.Select(t => new
            {
                o.PurchaseDate,
                t.TxnNumber,
                t.FirstName,
                t.LastName,
                t.Email,
                t.TicketTypeName,
                t.Amount,
                o.Currency,
                t.UsedAt
            })).ToList();
        if (ticketEvent == null || ordersWithTickets == null || !ordersWithTickets.Any())
            return NotFound();

        var fileName = $"{ticketEvent.Title}_Tickets-{DateTime.Now:yyyy_MM_dd-HH_mm_ss}.csv";
        var csvData = new StringBuilder();
        csvData.AppendLine("Purchase Date,Ticket Number,First Name,Last Name,Email,Ticket Tier,Amount,Currency,Attended Event");
        foreach (var ticket in ordersWithTickets)
        {
            csvData.AppendLine($"{ticket.PurchaseDate:MM/dd/yy HH:mm},{ticket.TxnNumber},{ticket.FirstName},{ticket.LastName},{ticket.Email},{ticket.TicketTypeName},{ticket.Amount},{ticket.Currency},{ticket.UsedAt.HasValue}");
        }
        byte[] fileBytes = Encoding.UTF8.GetBytes(csvData.ToString());
        return File(fileBytes, "text/csv", fileName);
    }

    private string GetUserId() => userManager.GetUserId(User);

    private UpdateSimpleTicketSalesEventViewModel TicketSalesEventToViewModel(Event entity)
    {
        return new UpdateSimpleTicketSalesEventViewModel
        {
            StoreId = entity.StoreId,   
            EventId = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Location = entity.Location,
            StartDate = entity.StartDate,
            Currency = entity.Currency,
            RedirectUrl = entity.RedirectUrl,
            EmailBody = entity.EmailBody,
            EndDate = entity.EndDate,
            EventType = entity.EventType,
            EmailSubject = entity.EmailSubject,
            HasMaximumCapacity = entity.HasMaximumCapacity,
            MaximumEventCapacity = entity.MaximumEventCapacity,
            ReminderEnabled = entity.ReminderEnabled,
            ReminderDaysBeforeEvent = entity.ReminderDaysBeforeEvent
        };
    }

    private Event TicketSalesEventViewModelToEntity(UpdateSimpleTicketSalesEventViewModel model, Event entity, string storeId)
    {
        void MapTo(Event e)
        {
            if (e.Id != null && e.StartDate != model.StartDate)
                e.ReminderSentAt = null;

            e.StoreId = storeId;
            e.Title = model.Title;
            e.Description = model.Description;
            e.Location = model.Location;
            e.StartDate = model.StartDate;
            e.EndDate = model.EndDate;
            e.Currency = model.Currency;
            e.EmailBody = model.EmailBody;
            e.EventType = model.EventType;
            e.RedirectUrl = model.RedirectUrl;
            e.EmailSubject = model.EmailSubject;
            e.HasMaximumCapacity = model.HasMaximumCapacity;
            e.MaximumEventCapacity = model.MaximumEventCapacity;
            e.ReminderEnabled = model.ReminderEnabled;
            e.ReminderDaysBeforeEvent = model.ReminderDaysBeforeEvent;

            if (!string.IsNullOrWhiteSpace(model.EventImageUrl))
                e.EventLogo = model.EventImageUrl;
        }

        if (entity is null)
        {
            var newEvent = new Event();
            MapTo(newEvent);
            return newEvent;
        }
        MapTo(entity);
        return entity;
    }
}

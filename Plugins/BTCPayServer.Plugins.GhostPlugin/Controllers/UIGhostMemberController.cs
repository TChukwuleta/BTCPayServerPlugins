using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Emails;
using BTCPayServer.Plugins.Emails.Controllers;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Services;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static BTCPayServer.Plugins.GhostPlugin.Services.EmailService;
using StoreData = BTCPayServer.Data.StoreData;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ghost/members/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostMemberController : Controller
{
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly GhostDbContextFactory _dbContextFactory;
    public UIGhostMemberController
        (EmailService emailService, 
        StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        EmailSenderFactory emailSenderFactory,
        GhostDbContextFactory dbContextFactory)
    {
        _storeRepo = storeRepo;
        _emailService = emailService;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _emailSenderFactory = emailSenderFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId, string filter)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        if (ghostSetting == null)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Error,
                Html = $"To manage ghost events, you need to set up Ghost credentials first",
                AllowDismiss = false
            });
            return RedirectToAction(nameof(UIGhostController.Index), "UIGhost", new { storeId });
        }

        var ghostMembers = ctx.GhostMembers.Where(c => c.StoreId == CurrentStore.Id && !string.IsNullOrEmpty(c.MemberId)).ToList();
        var ghostTransactions = ctx.GhostTransactions.AsNoTracking().Where(t => t.StoreId == CurrentStore.Id && t.TransactionStatus == TransactionStatus.Settled).ToList();

        var ghostPluginSetting = ghostSetting.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new GhostSettingsPageViewModel();
        var reminderDay = ghostPluginSetting?.ReminderStartDaysBeforeExpiration.GetValueOrDefault(4) switch
        {
            0 => 4,
            var value => value
        };

        var ghostMemberListViewModels = ghostMembers
            .Select(member =>
            {
                var transactions = ghostTransactions.Where(t => t.MemberId == member.Id).OrderByDescending(t => t.CreatedAt).ToList();
                var mostRecentTransaction = transactions.FirstOrDefault();
                return new GhostMemberListViewModel
                {
                    Id = member.Id,
                    MemberId = member.MemberId,
                    Name = member.Name,
                    Email = member.Email,
                    TierId = member.TierId,
                    StoreId = CurrentStore.Id,
                    ReminderDay = reminderDay.Value,
                    Frequency = member.Frequency,
                    CreatedDate = member.CreatedAt,
                    PeriodEndDate = (DateTimeOffset)(mostRecentTransaction?.PeriodEnd),
                    TierName = member.TierName,
                    Subscriptions = transactions.Select(t => new GhostTransactionViewModel
                    {
                        StoreId = CurrentStore.Id,
                        InvoiceId = t.InvoiceId, 
                        PaymentRequestId = t.PaymentRequestId,
                        InvoiceStatus = t.InvoiceStatus,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        MemberId = member.MemberId,
                        PeriodStartDate = t.PeriodStart,
                        PeriodEndDate = t.PeriodEnd
                    }).ToList()
                };
            }).ToList();

        var now = DateTime.UtcNow;
        var threeDaysFromNow = now.AddDays(3);

        var displayedMembers = ghostMemberListViewModels.Where(member =>
        {
            var periodEnd = member.PeriodEndDate.UtcDateTime;
            return filter switch
            {
                "expired" => now.Date >= periodEnd.Date,
                "active" => periodEnd.Date > threeDaysFromNow.Date,
                "aboutToExpire" => periodEnd.Date <= threeDaysFromNow.Date && periodEnd.Date > now.Date,
                _ => true
            };
        }).ToList();

        var emailSender = await _emailSenderFactory.GetEmailSender(storeId);
        var isEmailSettingsConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        ViewData["StoreEmailSettingsConfigured"] = isEmailSettingsConfigured;
        if (!isEmailSettingsConfigured)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Html = $"Kindly <a href='{Url.Action(action: nameof(UIStoresEmailController.StoreEmailSettings), controller: "UIStoresEmail",
                    values: new
                    {
                        area = EmailsPlugin.Area,
                        storeId = CurrentStore.Id
                    })}' class='alert-link'>configure Email SMTP</a> to be able to send reminder to subscribers",
                Severity = StatusMessageModel.StatusSeverity.Info,
                AllowDismiss = true
            });
        }

        return View(new GhostMembersViewModel { 
            Members = ghostMemberListViewModels, 
            DisplayedMembers = displayedMembers,
            Active = filter == "active",
            SoonToExpire = filter == "aboutToExpire",
            Expired = filter == "expired"
        });
    }


    [HttpGet("delete/{memberId}")]
    public async Task<IActionResult> Delete(string storeId, string memberId)
    {
        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var entity = ctx.GhostMembers.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == memberId);
        if (entity == null)
            return NotFound();

        return View("Confirm", new ConfirmModel($"Delete Member", $"Member ({entity.Name}) and all its transaction would also be deleted. Are you sure?", $"Delete {entity.Name}"));
    }


    [HttpPost("delete/{memberId}")]
    public async Task<IActionResult> DeletePost(string storeId, string memberId)
    {

        if (CurrentStore is null)
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var entity = ctx.GhostMembers.FirstOrDefault(c => c.StoreId == CurrentStore.Id && c.Id == memberId);
        if (entity == null)
            return NotFound();

        var txns = ctx.GhostTransactions.Where(c => c.StoreId == CurrentStore.Id && c.MemberId == memberId).ToList();
        if (txns.Any())
            ctx.GhostTransactions.RemoveRange(txns);  
        
        ctx.GhostMembers.Remove(entity);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Member deleted successfully";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }


    [HttpPost]
    public  IActionResult PreviewInvitationEmail(string storeId)
    {
        var templateContent = _emailService.GetEmbeddedResourceContent("Templates.SubscriptionExpirationReminder.cshtml");
        return Content(templateContent, "text/html");
    }


    [HttpGet("send-reminder/{memberId}")]
    public async Task<IActionResult> SendReminder(string storeId, string memberId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
        var member = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == memberId && c.StoreId == CurrentStore.Id);
        if (ghostSetting == null || member == null) return NotFound();

        var emailSender = await _emailSenderFactory.GetEmailSender(ghostSetting.StoreId);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Email settings not setup. Kindly configure Email SMTP in the admin settings";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }

        var latestTransaction = ctx.GhostTransactions
            .AsNoTracking().Where(t => t.MemberId == member.Id && t.TransactionStatus == TransactionStatus.Settled && DateTime.UtcNow >= t.PeriodStart)
            .OrderByDescending(t => t.CreatedAt).First();

        var emailRequest = new EmailRequest
        {
            StoreId = ghostSetting.StoreId,
            MemberId = member.Id,
            MemberName = member.Name,
            MemberEmail = member.Email,
            SubscriptionTier = member.TierName,
            ApiUrl = ghostSetting.ApiUrl,
            StoreName = ghostSetting.StoreName,
            SubscriptionUrl = $"{ghostSetting.BaseUrl}/plugins/{ghostSetting.StoreId}/ghost/api/subscription/{member.Id}/subscribe",
            ExpirationDate = latestTransaction.PeriodEnd,
        };
        try
        {
            await _emailService.SendMembershipSubscriptionReminderEmail(emailRequest);
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occured when sending subscription reminder. {ex.Message}";
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        TempData[WellKnownTempData.SuccessMessage] = $"Reminder has been sent to {member.Name}";
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }
}

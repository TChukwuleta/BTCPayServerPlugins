using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Services.Stores;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using System;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using Microsoft.AspNetCore.Routing;
using static BTCPayServer.Plugins.GhostPlugin.Services.EmailService;
using BTCPayServer.Services.Mails;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using Newtonsoft.Json;

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
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);

        var storeData = await _storeRepo.FindStore(storeId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());

        var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == storeId && !string.IsNullOrEmpty(c.MemberId)).ToList();
        var ghostTransactions = ctx.GhostTransactions.AsNoTracking().Where(t => t.StoreId == storeId && t.TransactionStatus == TransactionStatus.Success).ToList();

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
                    StoreId = storeId,
                    ReminderDay = reminderDay.Value,
                    Frequency = member.Frequency,
                    CreatedDate = member.CreatedAt,
                    PeriodEndDate = (DateTimeOffset)(mostRecentTransaction?.PeriodEnd),
                    TierName = member.TierName,
                    Subscriptions = transactions.Select(t => new GhostTransactionViewModel
                    {
                        StoreId = storeId,
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
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Kindly configure Email SMTP in the admin settings to be able to send reminder to subscribers",
                Severity = StatusMessageModel.StatusSeverity.Info
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
        if (ghostSetting == null || member == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var emailSender = await _emailSenderFactory.GetEmailSender(ghostSetting.StoreId);
        var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
        if (!isEmailConfigured)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"Email settings not setup. Kindly configure Email SMTP in the admin settings",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }

        var latestTransaction = ctx.GhostTransactions
            .AsNoTracking().Where(t => t.MemberId == member.Id && t.TransactionStatus == TransactionStatus.Success && DateTime.UtcNow >= t.PeriodStart)
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
        Console.WriteLine(emailRequest.SubscriptionUrl);
        try
        {
            await _emailService.SendMembershipSubscriptionReminderEmail(emailRequest);
        }
        catch (Exception ex)
        {
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Message = $"An error occured when sending subscription reminder. {ex.Message}",
                Severity = StatusMessageModel.StatusSeverity.Error
            });
            return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
        }
        TempData.SetStatusMessageModel(new StatusMessageModel()
        {
            Message = $"Reminder has been sent to {member.Name}",
            Severity = StatusMessageModel.StatusSeverity.Success
        });
        return RedirectToAction(nameof(List), new { storeId = CurrentStore.Id });
    }
}

using System;
using System.Collections.Generic;
using BTCPayServer.Client.Models;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.PaymentRequests;
using System.Threading.Tasks;
using System.Linq;
using BTCPayServer.Data;
using Newtonsoft.Json.Linq;
using BTCPayServer.Controllers;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Services.Invoices;
using System.Globalization;
using BTCPayServer.HostedServices;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;
using Newtonsoft.Json;
using static BTCPayServer.Plugins.GhostPlugin.Services.EmailService;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using BTCPayServer.Plugins.Emails.Services;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostPluginService : EventHostedServiceBase
{
    private readonly AppService _appService;
    private readonly StoreRepository _storeRepo;
    private readonly EmailService _emailService;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly UIInvoiceController _invoiceController;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PaymentRequestRepository _paymentRequestRepository;

    public GhostPluginService(
        AppService appService,
        StoreRepository storeRepo,
        EmailService emailService,
        EventAggregator eventAggregator,
        IHttpClientFactory clientFactory,
        ILogger<GhostPluginService> logger,
        InvoiceRepository invoiceRepository,
        EmailSenderFactory emailSenderFactory,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager,
        PaymentRequestRepository paymentRequestRepository) : base(eventAggregator, logger)
    {
        _storeRepo = storeRepo;
        _appService = appService;
        _userManager = userManager;
        _emailService = emailService;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
        _invoiceRepository = invoiceRepository;
        _emailSenderFactory = emailSenderFactory;
        _paymentRequestRepository = paymentRequestRepository;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        _ = ScheduleChecks();
    }

    private CancellationTokenSource _checkTcs = new();

    private async Task ScheduleChecks()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcs = new TaskCompletionSource<object>();
                PushEvent(new SequentialExecute(async () =>
                {
                    await HandleActiveSubscriptionCloseToEnding();
                    return null;
                }, tcs));
                await tcs.Task;
            }
            catch (Exception e)
            {
                Logs.PayServer.LogError(e, "Error while checking Ghost membership subscriptions");
            }
            _checkTcs = new CancellationTokenSource();
            _checkTcs.CancelAfter(TimeSpan.FromHours(1));
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), CancellationTokenSource.CreateLinkedTokenSource(_checkTcs.Token, CancellationToken).Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<SequentialExecute>();
        base.SubscribeToEvents();
    }

    public record SequentialExecute(Func<Task<object>> Action, TaskCompletionSource<object> TaskCompletionSource);

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is SequentialExecute sequentialExecute)
        {
            var task = await sequentialExecute.Action();
            sequentialExecute.TaskCompletionSource.SetResult(task);
            return;
        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    public async Task HandleActiveSubscriptionCloseToEnding()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var apps = (await _appService.GetApps(GhostApp.AppType)).Where(data => !data.Archived).ToList();
        List<(string ghostSettingId, string memberId, string email)> deliverRequests = new();
        foreach (var app in apps)
        {
            var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.AppId == app.Id);
            if (ghostSetting == null) continue;

            var emailSender = await _emailSenderFactory.GetEmailSender(ghostSetting.StoreId);
            var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
            if (!isEmailConfigured) continue;

            var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();
            if (ghostMembers == null || !ghostMembers.Any()) continue;

            var ghostPluginSetting = ghostSetting?.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new GhostSettingsPageViewModel();
            var automateReminder = ghostPluginSetting?.EnableAutomatedEmailReminders ?? false;
            if (!automateReminder) continue;

            var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
            var now = DateTimeOffset.UtcNow;
            var reminderDay = ghostPluginSetting?.ReminderStartDaysBeforeExpiration.GetValueOrDefault(4) switch
            {
                0 => 4,
                var value => value
            };


            foreach (var member in ghostMembers)
            {
                if (member.LastReminderSent.HasValue && member.LastReminderSent.Value.Date >= now.Date)
                    continue;

                var emailRequest = new EmailRequest
                {
                    StoreId = ghostSetting.StoreId,
                    MemberId = member.Id,
                    MemberName = member.Name,
                    MemberEmail = member.Email,
                    SubscriptionTier = member.TierName,
                    ApiUrl = ghostSetting.ApiUrl,
                    StoreName = ghostSetting.StoreName
                };
                try
                {
                    switch (member.Status)
                    {
                        case GhostSubscriptionStatus.New:
                            var firstTransaction = ctx.GhostTransactions.AsNoTracking().First(c => c.MemberId == member.Id && c.TransactionStatus == TransactionStatus.Settled);
                            emailRequest.ExpirationDate = firstTransaction.PeriodEnd;
                            var noticeFrame = firstTransaction.PeriodEnd - now;
                            if (noticeFrame.TotalDays <= reminderDay)
                            {
                                await SendReminderEmail(ghostSetting, member, firstTransaction.PeriodEnd, emailRequest);
                                member.LastReminderSent = DateTimeOffset.UtcNow;
                                ctx.Update(member);
                                await ctx.SaveChangesAsync();
                            }
                            break;

                        case GhostSubscriptionStatus.Renew:
                            var transactions = ctx.GhostTransactions.AsNoTracking().Where(p => p.MemberId == member.Id &&
                                p.TransactionStatus == TransactionStatus.Settled && !string.IsNullOrEmpty(p.PaymentRequestId)).ToList();

                            var currentPeriod = transactions.FirstOrDefault(p => p.PeriodStart.Date <= now.Date && p.PeriodEnd.Date >= now.Date);
                            var nextPeriod = transactions.FirstOrDefault(p => p.PeriodStart.Date > now.Date);
                            if (currentPeriod is null || nextPeriod is not null)
                                return;

                            var noticePeriod = currentPeriod.PeriodEnd - now;
                            emailRequest.ExpirationDate = currentPeriod.PeriodEnd;
                            if (noticePeriod.TotalDays <= reminderDay)
                            {
                                await SendReminderEmail(ghostSetting, member, currentPeriod.PeriodEnd, emailRequest);
                                member.LastReminderSent = DateTimeOffset.UtcNow;
                                ctx.Update(member);
                                await ctx.SaveChangesAsync();
                                deliverRequests.Add((ghostSetting.Id, member.Id, member.Email));
                            }
                            break;

                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logs.PayServer.LogError("Ghost Plugin: An error occurred while sending email to member {0}: {1} ", member.Email, ex);
                }
            }
        }
    }

    public async Task<bool> HasNotification(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var currentDate = DateTime.UtcNow;
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        var ghostPluginSetting = ghostSetting?.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new GhostSettingsPageViewModel();
        var reminderDay = ghostPluginSetting?.ReminderStartDaysBeforeExpiration.GetValueOrDefault(4) switch
        {
            0 => 4,
            var value => value
        };

        var latestTransactions = await ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == storeId && t.TransactionStatus == TransactionStatus.Settled)
            .GroupBy(t => t.MemberId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
            .ToListAsync();

        return latestTransactions.Any(t =>
            (currentDate >= t.PeriodStart && currentDate <= t.PeriodEnd &&
             t.PeriodEnd.AddDays(-reminderDay.Value) <= currentDate) || currentDate > t.PeriodEnd);
    }


    public async Task<PaymentRequestData> CreatePaymentRequest(GhostMember member, Tier tier, string appId, DateTimeOffset expiryDate)
    {
        // Amount is in lower denomination, so divide by 100
        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
        var pr = new PaymentRequestData()
        {
            StoreDataId = member.StoreId,
            Archived = false,
            Created = DateTimeOffset.UtcNow,
            Expiry = expiryDate,
            Currency = tier.currency,
            Amount = price,
            Status = PaymentRequestStatus.Pending
        };
        pr.SetBlob(new PaymentRequestBlob()
        {
            Description = $"{member.Name} Ghost membership renewal",
            Title = $"{member.Name} Ghost Subscription",
            Email = member.Email,
            AllowCustomPaymentAmounts = false
        });
        pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);
        return pr;
    }


    public async Task<InvoiceEntity> CreateMemberInvoiceAsync(BTCPayServer.Data.StoreData store, Tier tier, GhostMember member, string txnId, string url, string redirectUrl)
    {
        var ghostSearchTerm = $"{GhostApp.GHOST_PREFIX}{GhostApp.GHOST_MEMBER_ID_PREFIX}{member.Id}_{txnId}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = ghostSearchTerm,
            StoreId = new[] { store.Id }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(ghostSearchTerm).Any(s => s == member.Id.ToString())).ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        if (firstInvoiceSettled != null) return firstInvoiceSettled;

        // Amount is in lower denomination, so divided by 100
        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
        var invoiceRequest = new CreateInvoiceRequest()
        {
            Amount = price,
            Currency = tier.currency,
            Metadata = new JObject
            {
                ["MemberId"] = member.Id,
                ["TxnId"] = txnId
            },
            AdditionalSearchTerms = new[]
                {
                    member.Id.ToString(CultureInfo.InvariantCulture),
                    ghostSearchTerm
                }
        };
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            invoiceRequest.Checkout = new()
            {
                RedirectURL = redirectUrl
            };
        }
        var invoice = await _invoiceController.CreateInvoiceCoreRaw(invoiceRequest, store, url, new List<string>() { ghostSearchTerm });
        return invoice;
    }

    public async Task HandlePaidMembershipSubscription(string memberId, string paymentRequestId, string email)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var member = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == memberId);
        if (!string.Equals(member?.Email?.Trim(), email?.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == member.StoreId);
        var startDate = ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == member.StoreId && t.TransactionStatus == TransactionStatus.Settled && t.MemberId == memberId)
            .OrderByDescending(t => t.CreatedAt)
            .First().PeriodEnd;

        var start = DateOnly.FromDateTime(startDate);
        bool change = false;
        if (member != null)
        {
            var end = member.Frequency == TierSubscriptionFrequency.Monthly ? startDate.AddMonths(1) : startDate.AddYears(1);
            var existingPayment = ctx.GhostTransactions.AsNoTracking().First(p => p.PaymentRequestId == paymentRequestId);
            if (existingPayment is not null)
            {
                existingPayment.PeriodStart = startDate;
                existingPayment.PeriodEnd = end;
                existingPayment.TransactionStatus = TransactionStatus.Settled;
                ctx.Update(existingPayment);
                change = true;
            }
            if (member.Status == GhostSubscriptionStatus.New)
            {
                member.Status = GhostSubscriptionStatus.Renew;
                ctx.Update(member);
                change = true;
            }
            if (change)
            {
                ctx.SaveChanges();
            }
        }
    }

    private async Task SendReminderEmail(GhostSetting ghostSetting, GhostMember member, DateTime expirationDate, EmailRequest emailRequest)
    {
        var url = $"{ghostSetting.BaseUrl}/plugins/{ghostSetting.StoreId}/ghost/public/subscription/{member.Id}/subscribe";
        emailRequest.SubscriptionUrl = url;
        emailRequest.ExpirationDate = expirationDate;
        await _emailService.SendMembershipSubscriptionReminderEmail(emailRequest);

        var settingJson = JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) ?? new GhostSettingsPageViewModel();
        if (settingJson.SendReminderEmailsToAdmin)
        {
            var storeUser = await _storeRepo.GetStoreUser(ghostSetting.StoreId, ghostSetting.ApplicationUserId);
            var storeUserDetails = await _userManager.FindByIdAsync(storeUser.ApplicationUserId);
            await _emailService.SendMembershipReminderToAdmin(emailRequest, Defaults.AdminMembershipReminderEmailSubject, Defaults.AdminMembershipReminderEmailBody, storeUserDetails.Email);
        }
    }

    public record Defaults
    {
        public const string AdminMembershipReminderEmailSubject = @"Your invoice has been paid";

        public const string AdminMembershipReminderEmailBody = @"Hello {Name},

This is to inform you that a ghost subscriber with name {MemberName} and email {MemberEmail} subscription ends on {EndDate}.

Kindly proceed with further action. 

Thank you,
{StoreName} - Ghost Plugin";

    }
}

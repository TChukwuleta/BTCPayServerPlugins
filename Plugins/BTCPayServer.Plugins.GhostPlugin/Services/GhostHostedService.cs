using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.PaymentRequests;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static BTCPayServer.Plugins.GhostPlugin.Services.EmailService;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostHostedService : EventHostedServiceBase, IPeriodicTask
{
    private readonly AppService _appService;
    private readonly EmailService _emailService;
    private readonly StoreRepository _storeRepo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly PaymentRequestRepository _paymentRequestRepository;

    public GhostHostedService(EventAggregator eventAggregator,
        AppService appService,
        EmailService emailService,
        StoreRepository storeRepo,
        IServiceScopeFactory scopeFactory,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory httpClientFactory,
        GhostDbContextFactory dbContextFactory,
        PaymentRequestRepository paymentRequestRepository,
        Logs logs) : base(eventAggregator, logs)
    {
        _storeRepo = storeRepo;
        _appService = appService;
        _emailService = emailService;
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _httpClientFactory = httpClientFactory;
        _paymentRequestRepository = paymentRequestRepository;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        Subscribe<PaymentRequestEvent>();
        base.SubscribeToEvents();
    }

    public class CheckSubscriptionsEvent { }

    public async Task Do(CancellationToken cancellationToken)
    {
        PushEvent(new CheckSubscriptionsEvent());

    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case InvoiceEvent invoiceEvent when new[]
            {
            InvoiceEvent.MarkedCompleted,
            InvoiceEvent.MarkedInvalid,
            InvoiceEvent.Expired,
            InvoiceEvent.Confirmed,
            InvoiceEvent.Completed
        }.Contains(invoiceEvent.Name):
                {
                    var invoice = invoiceEvent.Invoice;
                    var ghostOrderId = invoice.GetInternalTags(GhostApp.GHOST_PREFIX).FirstOrDefault();
                    if (ghostOrderId != null)
                    {
                        bool? success = invoice.Status switch
                        {
                            InvoiceStatus.Settled => true,
                            InvoiceStatus.Invalid or
                            InvoiceStatus.Expired => false,
                            _ => (bool?)null
                        };
                        if (success.HasValue && ghostOrderId.StartsWith(GhostApp.GHOST_MEMBER_ID_PREFIX))
                        {
                            await RegisterMembershipCreationTransaction(invoice, ghostOrderId, success.Value);
                        }
                    }
                    break;
                }

            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged, Data.Status: PaymentRequestStatus.Completed } paymentRequestStatusUpdated:
                {
                    var prBlob = paymentRequestStatusUpdated.Data.GetBlob();

                    await using var ctx = _dbContextFactory.CreateContext();
                    var paymentRequestTransaction = ctx.GhostTransactions.FirstOrDefault(c => c.StoreId == paymentRequestStatusUpdated.Data.StoreDataId 
                        && c.PaymentRequestId == paymentRequestStatusUpdated.Data.Id);

                    if (paymentRequestTransaction == null) return;

                    await HandlePaidMembershipSubscription(paymentRequestTransaction, prBlob.Email);
                    break;
                }

            case CheckSubscriptionsEvent:
                {
                    await HandleActiveSubscriptionCloseToEnding();
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterMembershipCreationTransaction(InvoiceEntity invoice, string orderId, bool success)
    {
        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);

        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId);

        if (ghostSetting == null || !ghostSetting.CredentialsPopulated()) return;

        var transaction = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(c => c.StoreId == invoice.StoreId && c.InvoiceId == invoice.Id);
        if (transaction == null)
        {
            result.Write("Couldn't find a corresponding Ghost transaction table record", InvoiceEventData.EventSeverity.Error);
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
            return;
        }
        if (transaction.TransactionStatus != TransactionStatus.New)
        {
            result.Write("Transaction has previously been completed", InvoiceEventData.EventSeverity.Info);
            await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
            return;
        }

        transaction.InvoiceStatus = invoice.Status.ToString().ToLower();
        transaction.TransactionStatus = success ? TransactionStatus.Settled : TransactionStatus.Expired;
        if (success)
        {
            try
            {
                var ghostMember = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == transaction.MemberId);
                var expirationDate = ghostMember.Frequency == TierSubscriptionFrequency.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1);
                var client = new GhostAdminApiClient(_httpClientFactory, ghostSetting.CreateGhsotApiCredentials());
                var response = await client.CreateGhostMember(new CreateGhostMemberRequest
                {
                    members = new List<Member>
                        {
                            new Member
                            {
                                email = ghostMember.Email,
                                name = ghostMember.Name,
                                tiers = new List<MemberTier>
                                {
                                    new MemberTier
                                    {
                                        id = ghostMember.TierId,
                                        expiry_at = expirationDate.ToUniversalTime()
                                    }
                                }
                            }
                        }
                });
                if (response == null || response.members == null || response.members.Count == 0 || response.members[0] == null)
                {
                    result.Write($"Ghost response was null or empty when trying to create member", InvoiceEventData.EventSeverity.Error);
                    return;
                }
                transaction.PeriodStart = DateTime.UtcNow;
                transaction.PeriodEnd = expirationDate.ToUniversalTime();
                ghostMember.MemberId = response.members[0].id;
                ghostMember.MemberUuid = response.members[0].uuid;
                ghostMember.UnsubscribeUrl = response.members[0].unsubscribe_url;
                ctx.UpdateRange(ghostMember);
                result.Write($"Successfully created member with name: {ghostMember.Name} on Ghost.", InvoiceEventData.EventSeverity.Info);
            }
            catch (Exception ex)
            {
                result.Write($"Ghost error while trying to create member on Ghost platform. {ex.Message}", InvoiceEventData.EventSeverity.Error);
                Logs.PayServer.LogError(ex,
                    $"Ghost error while trying to create member on Ghost platform. {ex.Message}" +
                    $"Triggered by invoiceId: {invoice.Id}");
            }
        }
        ctx.UpdateRange(transaction);
        await ctx.SaveChangesAsync();
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
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

            if (!await _emailService.IsEmailSettingsConfigured(ghostSetting.StoreId)) continue;

            var ghostPluginSetting = ghostSetting?.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new();

            if (!(ghostPluginSetting?.EnableAutomatedEmailReminders ?? false)) continue;

            var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();
            if (!ghostMembers.Any()) continue;

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
                            var firstTransaction = ctx.GhostTransactions.AsNoTracking()
                                .First(c => c.MemberId == member.Id && c.TransactionStatus == TransactionStatus.Settled);

                            if ((firstTransaction.PeriodEnd - now).TotalDays > reminderDay) continue;

                            emailRequest.ExpirationDate = firstTransaction.PeriodEnd;
                            await SendReminderEmail(ghostSetting, member, firstTransaction.PeriodEnd, emailRequest);
                            member.LastReminderSent = DateTimeOffset.UtcNow;
                            ctx.Update(member);
                            break;

                        case GhostSubscriptionStatus.Renew:
                            var transactions = ctx.GhostTransactions.AsNoTracking().Where(p => p.MemberId == member.Id &&
                                p.TransactionStatus == TransactionStatus.Settled && !string.IsNullOrEmpty(p.PaymentRequestId)).ToList();

                            var currentPeriod = transactions.FirstOrDefault(p => p.PeriodStart.Date <= now.Date && p.PeriodEnd.Date >= now.Date);
                            var nextPeriod = transactions.FirstOrDefault(p => p.PeriodStart.Date > now.Date);
                            if (currentPeriod is null || nextPeriod is not null) continue;

                            if ((currentPeriod.PeriodEnd - now).TotalDays > reminderDay) continue;

                            emailRequest.ExpirationDate = currentPeriod.PeriodEnd;
                            await SendReminderEmail(ghostSetting, member, currentPeriod.PeriodEnd, emailRequest);
                            member.LastReminderSent = DateTimeOffset.UtcNow;
                            ctx.Update(member);
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

    private async Task SendReminderEmail(GhostSetting ghostSetting, GhostMember member, DateTime expirationDate, EmailRequest emailRequest)
    {
        var url = $"{ghostSetting.BaseUrl}/plugins/{ghostSetting.StoreId}/ghost/public/subscription/{member.Id}/subscribe";
        emailRequest.SubscriptionUrl = url;
        emailRequest.ExpirationDate = expirationDate;
        await _emailService.SendMembershipSubscriptionReminderEmail(emailRequest);

        var settingJson = JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) ?? new GhostSettingsPageViewModel();
        if (settingJson.SendReminderEmailsToAdmin)
        {
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var storeUser = await _storeRepo.GetStoreUser(ghostSetting.StoreId, ghostSetting.ApplicationUserId);
            var storeUserDetails = await userManager.FindByIdAsync(storeUser.ApplicationUserId);
            await _emailService.SendMembershipReminderToAdmin(emailRequest, Defaults.AdminMembershipReminderEmailSubject, Defaults.AdminMembershipReminderEmailBody, storeUserDetails.Email);
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
            Email = member.Email,
            AllowCustomPaymentAmounts = false
        });
        pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);
        return pr;
    }


    public async Task<InvoiceEntity> CreateMemberInvoice(BTCPayServer.Data.StoreData store, Tier tier, GhostMember member, string txnId, string url, string redirectUrl)
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
                    .Contains(entity.GetInvoiceState().Status.ToString().ToLower()));

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
        using var scope = _scopeFactory.CreateScope();
        var invoiceController = scope.ServiceProvider.GetRequiredService<UIInvoiceController>();
        var invoice = await invoiceController.CreateInvoiceCoreRaw(invoiceRequest, store, url, new List<string>() { ghostSearchTerm });
        return invoice;
    }

    public async Task HandlePaidMembershipSubscription(GhostTransaction ghostTransaction, string email)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var member = ctx.GhostMembers.AsNoTracking().First(c => c.Id == ghostTransaction.MemberId);
        if (!string.Equals(member.Email?.Trim(), email?.Trim(), StringComparison.OrdinalIgnoreCase)) return;

        var startDate = ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == member.StoreId && t.TransactionStatus == TransactionStatus.Settled && t.MemberId == ghostTransaction.MemberId)
            .OrderByDescending(t => t.CreatedAt).First().PeriodEnd;

        ghostTransaction.PeriodStart = startDate;
        ghostTransaction.PeriodEnd = member.Frequency == TierSubscriptionFrequency.Monthly ? startDate.AddMonths(1) : startDate.AddYears(1);
        ghostTransaction.TransactionStatus = TransactionStatus.Settled;
        ctx.Update(ghostTransaction);

        if (member.Status == GhostSubscriptionStatus.New)
        {
            member.Status = GhostSubscriptionStatus.Renew;
            ctx.Update(member);
        }
        await ctx.SaveChangesAsync();
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

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
using BTCPayServer.HostedServices.Webhooks;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;
using Newtonsoft.Json;
using static BTCPayServer.Plugins.GhostPlugin.Services.EmailService;
using BTCPayServer.Services.Mails;
using BTCPayServer.Events;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostPluginService : EventHostedServiceBase, IWebhookProvider
{
    private readonly AppService _appService;
    private readonly EmailService _emailService;
    private readonly WebhookSender _webhookSender;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly EmailSenderFactory _emailSenderFactory;
    private readonly UIInvoiceController _invoiceController;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly PaymentRequestRepository _paymentRequestRepository;

    public GhostPluginService(
        AppService appService,
        EmailService emailService,
        WebhookSender webhookSender,
        EventAggregator eventAggregator,
        IHttpClientFactory clientFactory,
        ILogger<GhostPluginService> logger,
        InvoiceRepository invoiceRepository,
        EmailSenderFactory emailSenderFactory,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        PaymentRequestRepository paymentRequestRepository) : base(eventAggregator, logger)
    {
        _appService = appService;
        _emailService = emailService;
        _webhookSender = webhookSender;
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

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        Subscribe<PaymentRequestEvent>();
        Subscribe<SequentialExecute>();
        base.SubscribeToEvents();
    }

    private CancellationTokenSource _checkTcs = new();
    public record SequentialExecute(Func<Task<object>> Action, TaskCompletionSource<object> TaskCompletionSource);

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case SequentialExecute sequentialExecute:
                {
                    var task = await sequentialExecute.Action();
                    sequentialExecute.TaskCompletionSource.SetResult(task);
                    return;
                }

            case InvoiceEvent invoiceEvent:
                {
                    await CreatePaymentRequestForActiveSubscriptionCloseToEnding();
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task ScheduleChecks()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcs = new TaskCompletionSource<object>();
                PushEvent(new SequentialExecute(async () =>
                {
                    await CreatePaymentRequestForActiveSubscriptionCloseToEnding();
                    return null;
                }, tcs));
                await tcs.Task;
            }
            catch (Exception e)
            {
                Logs.PayServer.LogError(e, "Error while checking subscriptions");
            }
            _checkTcs = new CancellationTokenSource();
            _checkTcs.CancelAfter(TimeSpan.FromHours(1));

            try
            {
                await Task.Delay(TimeSpan.FromHours(1),
                    CancellationTokenSource.CreateLinkedTokenSource(_checkTcs.Token, CancellationToken).Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public async Task CreatePaymentRequestForActiveSubscriptionCloseToEnding()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var apps = (await _appService.GetApps(GhostApp.AppType)).Where(data => !data.Archived).ToList();
        List<(string ghostSettingId, string memberId, string email)> deliverRequests = new();
        foreach (var app in apps)
        {
            var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.AppId == app.Id);
            if (ghostSetting == null) continue;

            var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();
            if (!ghostMembers.Any()) continue;

            var ghostPluginSetting = ghostSetting.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new GhostSettingsPageViewModel();
            var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
            var now = DateTimeOffset.UtcNow;
            var reminderDay = ghostPluginSetting?.ReminderStartDaysBeforeExpiration.GetValueOrDefault(4) switch
            {
                0 => 4,
                var value => value
            };

            foreach (var member in ghostMembers)
            {
                var emailSender = await _emailSenderFactory.GetEmailSender(ghostSetting.StoreId);
                var isEmailConfigured = (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
                if (!isEmailConfigured || (member.LastReminderSent != null && member.LastReminderSent.Value.Date >= now.Date))
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
                            var firstTransaction = ctx.GhostTransactions.AsNoTracking().First(c => c.MemberId == member.Id && c.TransactionStatus == TransactionStatus.Success);
                            var noticeFrame = firstTransaction.PeriodEnd - now;
                            if (noticeFrame.TotalDays <= reminderDay)
                            {
                                Console.WriteLine("Train train");
                                await SendReminderEmail(ghostSetting, member, firstTransaction.PeriodEnd, emailRequest);
                                member.LastReminderSent = DateTimeOffset.UtcNow;
                                ctx.Update(member);
                                await ctx.SaveChangesAsync();
                            }
                            break;

                        case GhostSubscriptionStatus.Renew:
                            var transactions = ctx.GhostTransactions.AsNoTracking().Where(p => p.MemberId == member.Id &&
                                p.TransactionStatus == TransactionStatus.Success && !string.IsNullOrEmpty(p.PaymentRequestId)).ToList();

                            var currentPeriod = transactions.FirstOrDefault(p => p.PeriodStart <= now && p.PeriodEnd >= now);
                            var nextPeriod = transactions.FirstOrDefault(p => p.PeriodStart > now);
                            if (currentPeriod is null || nextPeriod is not null)
                                return;

                            Console.WriteLine("I am a moving train");

                            var noticePeriod = currentPeriod.PeriodEnd - now;
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
                    Console.WriteLine($"Error sending an email: {ex.Message}");
                }
            }
            foreach (var deliverRequest in deliverRequests)
            {
                var webhooks = await _webhookSender.GetWebhooks(app.StoreDataId, GhostApp.GhostSubscriptionRenewalRequested);
                foreach (var webhook in webhooks)
                {
                    _webhookSender.EnqueueDelivery(CreateSubscriptionRenewalRequestedDeliveryRequest(webhook, app.Id, app.StoreDataId, deliverRequest.memberId,
                         deliverRequest.email));
                }
                EventAggregator.Publish(CreateSubscriptionRenewalRequestedDeliveryRequest(null, app.Id, app.StoreDataId, deliverRequest.memberId,
                    deliverRequest.email));
            }
        }
    }

    public async Task<bool> HasNotification(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var currentDate = DateTime.UtcNow;
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        var ghostPluginSetting = ghostSetting.Setting != null ? JsonConvert.DeserializeObject<GhostSettingsPageViewModel>(ghostSetting.Setting) : new GhostSettingsPageViewModel();
        var reminderDay = ghostPluginSetting?.ReminderStartDaysBeforeExpiration.GetValueOrDefault(4) switch
        {
            0 => 4,
            var value => value
        };

        var latestTransactions = await ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == storeId && t.TransactionStatus == TransactionStatus.Success)
            .GroupBy(t => t.MemberId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
            .ToListAsync();

        return latestTransactions.Any(t =>
            (currentDate >= t.PeriodStart && currentDate <= t.PeriodEnd &&
             t.PeriodEnd.AddDays(-reminderDay.Value) <= currentDate) || currentDate > t.PeriodEnd);
    }


    public async Task<BTCPayServer.Data.PaymentRequestData> CreatePaymentRequest(GhostMember member, Tier tier, string appId, DateTimeOffset expiryDate)
    {
        // Amount is in lower denomination, so divide by 100
        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
        var pr = new BTCPayServer.Data.PaymentRequestData()
        {
            StoreDataId = member.StoreId,
            Archived = false,
            Created = DateTimeOffset.UtcNow,
            Status = Client.Models.PaymentRequestData.PaymentRequestStatus.Pending
        };
        pr.SetBlob(new CreatePaymentRequestRequest()
        {
            StoreId = member.StoreId,
            Amount = price,
            Currency = tier.currency,
            ExpiryDate = expiryDate,
            Description = $"{member.Name} Ghost membership renewal",
            Title = $"{member.Name} Ghost Subscription",
            Email = member.Email,
            AllowCustomPaymentAmounts = false,
            AdditionalData = new Dictionary<string, JToken>()
            {
                {"source", JToken.FromObject(GhostApp.AppName)},
                {"memberId", JToken.FromObject(member.Id)},
                {"storeId", JToken.FromObject(member.StoreId)},
                {"appId", JToken.FromObject(appId)}
            }
        });
        pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);
        return pr;
    }


    public async Task<InvoiceEntity> CreateInvoiceAsync(BTCPayServer.Data.StoreData store, Tier tier, GhostMember member, string txnId, string url)
    {
        var ghostSearchTerm = $"{GhostApp.GHOST_MEMBER_ID_PREFIX}{member.Id}_{txnId}";
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

        if (firstInvoiceSettled != null)
            return firstInvoiceSettled;

        // Amount is in lower denomination, so divided by 100
        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
            new CreateInvoiceRequest()
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
            }, store, url, new List<string>() { ghostSearchTerm });

        return invoice;
    }

    public async Task HandlePaidMembershipSubscription(PaymentRequestBaseData pr, string memberId, string paymentRequestId, string email)
    {
        await using var ctx = _dbContextFactory.CreateContext();

        var member = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == memberId);
        if (!string.Equals(member?.Email?.Trim(), email?.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == member.StoreId);
        var startDate = pr.ExpiryDate.HasValue ? pr.ExpiryDate.Value.UtcDateTime : (ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == member.StoreId && t.TransactionStatus == TransactionStatus.Success && t.MemberId == memberId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault()).PeriodEnd;

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
                existingPayment.TransactionStatus = TransactionStatus.Success;
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

    GhostSubscriptionWebhookDeliveryRequest CreateSubscriptionRenewalRequestedDeliveryRequest(WebhookData? webhook,
        string ghostSettingId, string storeId, string memberId, string email)
    {
        var webhookEvent = new WebhookSubscriptionEvent(GhostApp.GhostSubscriptionRenewalRequested, storeId)
        {
            WebhookId = webhook?.Id,
            GhostSettingId = ghostSettingId,
            MemberId = memberId,
            Email = email
        };
        var delivery = webhook is null ? null : WebhookExtensions.NewWebhookDelivery(webhook.Id);
        if (delivery is not null)
        {
            webhookEvent.DeliveryId = delivery.Id;
            webhookEvent.OriginalDeliveryId = delivery.Id;
            webhookEvent.Timestamp = delivery.Timestamp;
        }
        return new GhostSubscriptionWebhookDeliveryRequest(webhook?.Id, webhookEvent, delivery, webhook?.GetBlob());
    }


    public class WebhookSubscriptionEvent : StoreWebhookEvent
    {
        public WebhookSubscriptionEvent(string type, string storeId)
        {
            if (!type.StartsWith("ghost", StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException("Invalid event type", nameof(type));
            Type = type;
            StoreId = storeId;
        }
        [JsonProperty(Order = 2)] public string GhostSettingId { get; set; }
        [JsonProperty(Order = 3)] public string MemberId { get; set; }
        [JsonProperty(Order = 4)] public string Status { get; set; }
        [JsonProperty(Order = 6)] public string Email { get; set; }
    }


    public class GhostSubscriptionWebhookDeliveryRequest(string? webhookId,
        WebhookSubscriptionEvent webhookEvent, BTCPayServer.Data.WebhookDeliveryData? delivery, WebhookBlob? webhookBlob)
        : WebhookSender.WebhookDeliveryRequest(webhookId!, webhookEvent, delivery!, webhookBlob!)
    {
        public override Task<SendEmailRequest?> Interpolate(SendEmailRequest req,
            UIStoresController.StoreEmailRule storeEmailRule)
        {
            if (storeEmailRule.CustomerEmail &&
                MailboxAddressValidator.TryParse(webhookEvent.Email, out var bmb))
            {
                req.Email ??= string.Empty;
                req.Email += $",{bmb}";
            }

            req.Subject = Interpolate(req.Subject);
            req.Body = Interpolate(req.Body);
            return Task.FromResult(req)!;
        }

        private string Interpolate(string str)
        {
            var res = str.Replace("{Ghost.MemberId}", webhookEvent.MemberId)
                .Replace("{Ghost.Status}", webhookEvent.Status)
                .Replace("{Ghost.GhostSettingId}", webhookEvent.GhostSettingId);
            return res;
        }
    }

    public Dictionary<string, string> GetSupportedWebhookTypes()
    {
        return new Dictionary<string, string>
        {
            {GhostApp.GhostSubscriptionRenewalRequested, "A subscription has generated a payment request for ghost membership renewal"}
        };
    }

    public WebhookEvent CreateTestEvent(string type, params object[] args)
    {
        var storeId = args[0].ToString();
        return new WebhookSubscriptionEvent(type, storeId)
        {
            GhostSettingId = "__test__" + Guid.NewGuid() + "__test__",
            MemberId = "__test__" + Guid.NewGuid() + "__test__",
            Status = GhostSubscriptionStatus.New.ToString()
        };
    }

    private async Task SendReminderEmail(GhostSetting ghostSetting, GhostMember member, DateTime expirationDate, EmailRequest emailRequest)
    {
        var url = $"{ghostSetting.BaseUrl}/plugins/{ghostSetting.StoreId}/ghost/api/subscription/{member.Id}/subscribe";
        emailRequest.SubscriptionUrl = url;
        emailRequest.ExpirationDate = expirationDate;
        await _emailService.SendMembershipSubscriptionReminderEmail(emailRequest);
    }
}

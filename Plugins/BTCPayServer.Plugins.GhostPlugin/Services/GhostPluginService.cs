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

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class GhostPluginService : EventHostedServiceBase, IWebhookProvider
{
    private readonly AppService _appService;
    private readonly WebhookSender _webhookSender;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly PaymentRequestRepository _paymentRequestRepository;

    public GhostPluginService(
        AppService appService,
        WebhookSender webhookSender,
        EventAggregator eventAggregator,
        IHttpClientFactory clientFactory,
        ILogger<GhostPluginService> logger,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        PaymentRequestRepository paymentRequestRepository) : base(eventAggregator, logger)
    {
        _appService = appService;
        _webhookSender = webhookSender;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
        _invoiceRepository = invoiceRepository;
        _paymentRequestRepository = paymentRequestRepository;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        _ = ScheduleChecks();
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<PaymentRequestEvent>();
        Subscribe<SequentialExecute>();
        base.SubscribeToEvents();
    }

    public record SequentialExecute(Func<Task<object>> Action, TaskCompletionSource<object> TaskCompletionSource);

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case SequentialExecute sequentialExecute:
                {
                    Console.WriteLine("Hello from this side");
                    var task = await sequentialExecute.Action();
                    sequentialExecute.TaskCompletionSource.SetResult(task);
                    return;
                }
            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged } paymentRequestStatusUpdated:
                {
                    Console.WriteLine("wRITING LINE");
                    /*var prBlob = paymentRequestStatusUpdated.Data.GetBlob();
                    prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestSourceKey, out var src);
                    prBlob.AdditionalData.TryGetValue(GhostApp.MemberIdKey, out var memberIdToken);
                    if (src == null || src.Value<string>() != GhostApp.AppName || memberIdToken == null)
                        return;

                    if (paymentRequestStatusUpdated.Data.Status == Client.Models.PaymentRequestData.PaymentRequestStatus.Completed)
                    {
                        var memberId = memberIdToken?.Value<string>();
                        var blob = paymentRequestStatusUpdated.Data.GetBlob();
                        var memberEmail = blob.Email;

                        await HandlePaidMembershipSubscription(memberId, paymentRequestStatusUpdated.Data.Id, memberEmail);
                    }*/
                    await _checkTcs.CancelAsync();
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
                await CreatePaymentRequestForActiveSubscriptionCloseToEnding();
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

    private CancellationTokenSource _checkTcs = new();

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
        // return RedirectToAction("ViewPaymentRequest", "UIPaymentRequest", new {payReqId = pr.Id});
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
        var price = member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price;
        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
            new CreateInvoiceRequest()
            {
                Amount = price / 100,
                Currency = tier.currency,
                Metadata = new JObject
                {
                    ["MemberId"] = member.Id
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

        Console.WriteLine(pr.ExpiryDate);
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == member.StoreId);
        var startDate = pr.ExpiryDate.HasValue ? pr.ExpiryDate.Value.UtcDateTime : (ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == member.StoreId && t.TransactionStatus == TransactionStatus.Success && t.MemberId == memberId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault()).PeriodEnd;

        var start = DateOnly.FromDateTime(startDate);
        bool change = false;
        if (member != null)
        {
            var end = member.Frequency == TierSubscriptionFrequency.Monthly ? start.AddMonths(1).ToDateTime(TimeOnly.MaxValue) : start.AddYears(1).ToDateTime(TimeOnly.MaxValue);
            var existingPayment = ctx.GhostTransactions.AsNoTracking().First(p => p.PaymentRequestId == paymentRequestId);
            Console.WriteLine(JsonConvert.SerializeObject(existingPayment, Formatting.Indented));
            if (existingPayment is not null)
            {
                existingPayment.PeriodStart = start.ToDateTime(TimeOnly.MinValue);
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


    public async Task CreatePaymentRequestForActiveSubscriptionCloseToEnding()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var tcs = new TaskCompletionSource<object>();
        PushEvent(new SequentialExecute(async () =>
        {
            var apps = await _appService.GetApps(GhostApp.AppType);
            apps = apps.Where(data => !data.Archived).ToList();
            Console.WriteLine(JsonConvert.SerializeObject(apps));
            List<(string ghostSettingId, string memberId, string paymentRequestId, string email)> deliverRequests = new();
            foreach (var app in apps)
            {
                var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.AppId == app.Id);
                if (ghostSetting == null)
                    continue;

                Console.WriteLine($"Ghost setting: {ghostSetting.StoreName}");
                var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
                var ghostTiers = await apiClient.RetrieveGhostTiers();
                var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == ghostSetting.StoreId).ToList();
                if (ghostMembers.Any() is true)
                {
                    foreach (var member in ghostMembers)
                    {
                        Tier tier = ghostTiers.FirstOrDefault(c => c.id == member.TierId);
                        if (tier == null)
                            continue;

                        var price = Convert.ToDecimal(member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
                        var pr = new BTCPayServer.Data.PaymentRequestData()
                        {
                            StoreDataId = ghostSetting.StoreId,
                            Status = Client.Models.PaymentRequestData.PaymentRequestStatus.Pending,
                            Created = DateTimeOffset.UtcNow,
                            Archived = false
                        };

                        switch (member.Status)
                        {
                            case GhostSubscriptionStatus.New:
                                GhostTransaction txnPeriod = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(p =>
                                p.TransactionStatus == TransactionStatus.Success && p.MemberId == member.Id &&
                                p.PeriodStart <= DateTimeOffset.UtcNow &&
                                p.PeriodEnd >= DateTimeOffset.UtcNow);

                                //there should only ever be one future payment request at a time
                                GhostTransaction nextTxnPeriod = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(p =>
                                p.PeriodStart > DateTimeOffset.UtcNow && p.MemberId == member.Id);
                                if (txnPeriod is null || nextTxnPeriod is not null)
                                    continue;

                                var firstTransaction = ctx.GhostTransactions.AsNoTracking().First(c => c.MemberId == member.Id);
                                pr = await CreatePaymentRequest(member, tier, app.Id, firstTransaction.PeriodEnd);
                                break;

                            case GhostSubscriptionStatus.Renew:
                                GhostTransaction currentPeriod = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(p => 
                                p.TransactionStatus == TransactionStatus.Success && p.MemberId == member.Id &&
                                p.PeriodStart <= DateTimeOffset.UtcNow &&
                                p.PeriodEnd >= DateTimeOffset.UtcNow && !string.IsNullOrEmpty(p.PaymentRequestId));

                                //there should only ever be one future payment request at a time
                                GhostTransaction nextPeriod = ctx.GhostTransactions.AsNoTracking().FirstOrDefault(p => 
                                p.PeriodStart > DateTimeOffset.UtcNow && p.MemberId == member.Id &&
                                !string.IsNullOrEmpty(p.PaymentRequestId));
                                if (currentPeriod is null || nextPeriod is not null)
                                    continue;

                                var noticePeriod = currentPeriod.PeriodEnd - DateTimeOffset.UtcNow;

                                var lastPr = await _paymentRequestRepository.FindPaymentRequest(currentPeriod.PaymentRequestId, null, CancellationToken.None);
                                var lastBlob = lastPr.GetBlob();

                                if (noticePeriod.Days <= 3)
                                {
                                    pr.SetBlob(new PaymentRequestBaseData()
                                    {
                                        ExpiryDate = currentPeriod.PeriodEnd,
                                        Amount = price,
                                        Currency = tier.currency,
                                        StoreId = ghostSetting.StoreId,
                                        Title = $"{member.Name} Subscription Renewal",
                                        Description = $"{member.Name} Subscription Renewal",
                                        AdditionalData = lastBlob.AdditionalData
                                    });
                                    pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);

                                    var start = DateOnly.FromDateTime(currentPeriod.PeriodEnd.AddDays(1));
                                    var end = member.Frequency == TierSubscriptionFrequency.Monthly ? start.AddMonths(1) : start.AddYears(1);
                                    GhostTransaction transaction = new GhostTransaction
                                    {
                                        PaymentRequestId = pr.Id,
                                        StoreId = ghostSetting.StoreId,
                                        MemberId = member.Id,
                                        TransactionStatus = TransactionStatus.Pending,
                                        TierId = member.TierId,
                                        Frequency = member.Frequency,
                                        CreatedAt = DateTime.UtcNow,
                                        PeriodStart = start.ToDateTime(TimeOnly.MinValue),
                                        PeriodEnd = end.ToDateTime(TimeOnly.MinValue),
                                        Amount = price
                                    };
                                    ctx.Add(transaction);
                                    await ctx.SaveChangesAsync();
                                    deliverRequests.Add((ghostSetting.Id, member.Id, pr.Id, member.Email));
                                }
                                break;

                            default:
                                break;
                        }
                    }

                }
                foreach (var deliverRequest in deliverRequests)
                {
                    var webhooks = await _webhookSender.GetWebhooks(app.StoreDataId, GhostApp.GhostSubscriptionRenewalRequested);
                    foreach (var webhook in webhooks)
                    {
                        _webhookSender.EnqueueDelivery(CreateSubscriptionRenewalRequestedDeliveryRequest(webhook, app.Id, app.StoreDataId, deliverRequest.memberId, 
                            deliverRequest.paymentRequestId, deliverRequest.email));
                    }

                    EventAggregator.Publish(CreateSubscriptionRenewalRequestedDeliveryRequest(null, app.Id, app.StoreDataId, deliverRequest.memberId, 
                        deliverRequest.paymentRequestId, deliverRequest.email));
                }
            }

            return null;
        }, tcs));
        await tcs.Task;
    }

    public async Task<bool> HasNotification(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var currentDate = DateTime.UtcNow;

        var latestTransactions = await ctx.GhostTransactions
            .AsNoTracking().Where(t => t.StoreId == storeId && t.TransactionStatus == Data.TransactionStatus.Success)
            .GroupBy(t => t.MemberId)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
            .ToListAsync();

        return latestTransactions.Any(t =>
            (currentDate >= t.PeriodStart && currentDate <= t.PeriodEnd &&
             t.PeriodEnd.AddDays(-2) <= currentDate) || currentDate > t.PeriodEnd);
    }


    GhostSubscriptionWebhookDeliveryRequest CreateSubscriptionRenewalRequestedDeliveryRequest(WebhookData? webhook,
        string ghostSettingId, string storeId, string memberId, string paymentRequestId, string email)
    {
        var webhookEvent = new WebhookSubscriptionEvent(GhostApp.GhostSubscriptionRenewalRequested, storeId)
        {
            WebhookId = webhook?.Id,
            GhostSettingId = ghostSettingId,
            MemberId = memberId,
            PaymentRequestId = paymentRequestId,
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
        [JsonProperty(Order = 5)] public string PaymentRequestId { get; set; }
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
                .Replace("{Ghost.PaymentRequestId}", webhookEvent.PaymentRequestId)
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
}

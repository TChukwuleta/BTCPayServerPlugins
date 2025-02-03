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
using AngleSharp.Dom;
using BTCPayServer.HostedServices.Webhooks;
using TransactionStatus = BTCPayServer.Plugins.GhostPlugin.Data.TransactionStatus;
using Newtonsoft.Json;
using BTCPayServer.Services;

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
    private readonly BTCPayNetworkJsonSerializerSettings _btcPayNetworkJsonSerializerSettings;

    public GhostPluginService(
        AppService appService,
        WebhookSender webhookSender,
        EventAggregator eventAggregator,
        IHttpClientFactory clientFactory,
        ILogger<GhostPluginService> logger,
        InvoiceRepository invoiceRepository,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        PaymentRequestRepository paymentRequestRepository,
        BTCPayNetworkJsonSerializerSettings btcPayNetworkJsonSerializerSettings) : base(eventAggregator, logger)
    {
        _appService = appService;
        _webhookSender = webhookSender;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
        _invoiceRepository = invoiceRepository;
        _paymentRequestRepository = paymentRequestRepository;
        _btcPayNetworkJsonSerializerSettings = btcPayNetworkJsonSerializerSettings;
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
                    var task = await sequentialExecute.Action();
                    sequentialExecute.TaskCompletionSource.SetResult(task);
                    return;
                }
            case PaymentRequestEvent { Type: PaymentRequestEvent.StatusChanged } paymentRequestStatusUpdated:
                {
                    var prBlob = paymentRequestStatusUpdated.Data.GetBlob();
                    if (!prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestSourceKey, out var src) ||
                        src.Value<string>() != GhostApp.AppName ||
                        !prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestAppId, out var subscriptionAppidToken) ||
                        subscriptionAppidToken.Value<string>() is not { } subscriptionAppId)
                    {
                        return;
                    }

                    var isNew = !prBlob.AdditionalData.TryGetValue(GhostApp.PaymentRequestSubscriptionIdKey, out var subscriptionIdToken);

                    prBlob.AdditionalData.TryGetValue(GhostApp.MemberIdKey, out var memberIdToken);

                    if (isNew && paymentRequestStatusUpdated.Data.Status !=
                        Client.Models.PaymentRequestData.PaymentRequestStatus.Completed)
                    {
                        return;
                    }

                    if (paymentRequestStatusUpdated.Data.Status == Client.Models.PaymentRequestData.PaymentRequestStatus.Completed)
                    {
                        var memberId = memberIdToken?.Value<string>();
                        var blob = paymentRequestStatusUpdated.Data.GetBlob();
                        var memberEmail = blob.Email;

                        await HandlePaidMembershipSubscription(subscriptionAppId, memberId, paymentRequestStatusUpdated.Data.Id, memberEmail);
                    }
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

    public async Task<InvoiceEntity> CreateInvoiceAsync(BTCPayServer.Data.StoreData store, Tier tier, GhostMember member, string url)
    {
        var ghostSearchTerm = $"{GhostApp.GHOST_MEMBER_ID_PREFIX}{member.Id}";
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

    public async Task HandlePaidMembershipSubscription(string appId, string memberId, string paymentRequestId, string email)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var app = await _appService.GetApp(appId, GhostApp.AppType, false, true);
        if (app == null)
            return;

        var settings = app.GetSettings<GhostSetting>();
        var start = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);

        if (!settings.Members.TryGetValue(memberId, out var member))
        {
            var end = member.Frequency == TierSubscriptionFrequency.Monthly ? start.AddMonths(1).ToDateTime(TimeOnly.MaxValue) : start.AddYears(1).ToDateTime(TimeOnly.MaxValue);
            var existingPayment = member.GhostTransactions.First(p => p.PaymentRequestId == paymentRequestId);
            if (existingPayment is null)
            {
                GhostTransaction transaction = new GhostTransaction
                {
                    StoreId = member.StoreId,
                    PaymentRequestId = paymentRequestId,
                    MemberId = member.Id,
                    TransactionStatus = TransactionStatus.Success,
                    TierId = member.TierId,
                    Frequency = member.Frequency,
                    CreatedAt = DateTime.UtcNow,
                    PeriodStart = start.ToDateTime(TimeOnly.MinValue),
                    PeriodEnd = end
                };
                ctx.UpdateRange(transaction);
                member.GhostTransactions.Add(transaction);
            }
            else
            {
                existingPayment.TransactionStatus = TransactionStatus.Success;
                ctx.UpdateRange(existingPayment);
            }
            if (member.Status == GhostSubscriptionStatus.New)
            {
                member.Status = GhostSubscriptionStatus.Renew;
                ctx.UpdateRange(member);
            }
            ctx.SaveChanges();
        }
        app.SetSettings(settings);
        await _appService.UpdateOrCreateApp(app);

        // Handle webhook
    }


    public async Task CreatePaymentRequestForActiveSubscriptionCloseToEnding()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var tcs = new TaskCompletionSource<object>();

        PushEvent(new SequentialExecute(async () =>
        {
            var apps = await _appService.GetApps(GhostApp.AppType);
            apps = apps.Where(data => !data.Archived).ToList();
            List<(string appId, string memberId, string paymentRequestId, string email)> deliverRequests = new();
            foreach (var app in apps)
            {
                var settings = app.GetSettings<GhostSetting>();
                if (settings.Members?.Any() is true)
                {
                    foreach (var member in settings.Members)
                    {
                        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == member.Value.StoreId);
                        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
                        var ghostTiers = await apiClient.RetrieveGhostTiers();
                        Tier tier = ghostTiers.FirstOrDefault(c => c.id == member.Value.TierId);
                        if (tier == null)
                            continue;

                        var price = Convert.ToDecimal(member.Value.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price) / 100;
                        var pr = new BTCPayServer.Data.PaymentRequestData()
                        {
                            StoreDataId = ghostSetting.StoreId,
                            Status = Client.Models.PaymentRequestData.PaymentRequestStatus.Pending,
                            Created = DateTimeOffset.UtcNow,
                            Archived = false
                        };

                        switch (member.Value.Status)
                        {
                            case GhostSubscriptionStatus.New:
                                var firstTransaction = member.Value.GhostTransactions.First();
                                pr = await CreatePaymentRequest(member.Value, tier, app.Id, firstTransaction.PeriodEnd);
                                break;

                            case GhostSubscriptionStatus.Renew:
                                GhostTransaction currentPeriod = member.Value.GhostTransactions.FirstOrDefault(p => p.TransactionStatus == Data.TransactionStatus.Success &&
                                p.PeriodStart <= DateTimeOffset.UtcNow &&
                                p.PeriodEnd >= DateTimeOffset.UtcNow && !string.IsNullOrEmpty(p.PaymentRequestId));

                                //there should only ever be one future payment request at a time
                                GhostTransaction nextPeriod = member.Value.GhostTransactions.FirstOrDefault(p => p.PeriodStart > DateTimeOffset.UtcNow && !string.IsNullOrEmpty(p.PaymentRequestId));
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
                                        Title = $"{member.Value.Name} Subscription Renewal",
                                        Description = $"{member.Value.Name} Subscription Renewal",
                                        AdditionalData = lastBlob.AdditionalData
                                    });
                                    pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);

                                    var start = DateOnly.FromDateTime(currentPeriod.PeriodEnd.AddDays(1));
                                    var end = member.Value.Frequency == TierSubscriptionFrequency.Monthly ? start.AddMonths(1) : start.AddYears(1);
                                    GhostTransaction transaction = new GhostTransaction
                                    {
                                        PaymentRequestId = pr.Id,
                                        StoreId = ghostSetting.StoreId,
                                        MemberId = member.Value.Id,
                                        TransactionStatus = TransactionStatus.Pending,
                                        TierId = member.Value.TierId,
                                        Frequency = member.Value.Frequency,
                                        CreatedAt = DateTime.UtcNow,
                                        PeriodStart = start.ToDateTime(TimeOnly.MinValue),
                                        PeriodEnd = end.ToDateTime(TimeOnly.MinValue),
                                        Amount = price
                                    };
                                    ctx.Add(transaction);
                                    await ctx.SaveChangesAsync();
                                    member.Value.GhostTransactions.Add(transaction);
                                    deliverRequests.Add((app.Id, member.Value.Id, pr.Id, member.Value.Email));
                                }
                                break;

                            default:
                                break;
                        }

                        app.SetSettings(settings);
                        await _appService.UpdateOrCreateApp(app);
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


    GhostSubscriptionWebhookDeliveryRequest CreateSubscriptionRenewalRequestedDeliveryRequest(WebhookData? webhook,
        string appId, string storeId, string memberId, string paymentRequestId, string email)
    {
        var webhookEvent = new WebhookSubscriptionEvent(GhostApp.GhostSubscriptionRenewalRequested, storeId)
        {
            WebhookId = webhook?.Id,
            AppId = appId,
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
        [JsonProperty(Order = 2)] public string AppId { get; set; }
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
                .Replace("{Ghost.AppId}", webhookEvent.AppId);
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
            AppId = "__test__" + Guid.NewGuid() + "__test__",
            MemberId = "__test__" + Guid.NewGuid() + "__test__",
            Status = GhostSubscriptionStatus.New.ToString()
        };
    }
}

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

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostPluginService : EventHostedServiceBase, IWebhookProvider
    {
        private readonly AppService _appService;
        private readonly IHttpClientFactory _clientFactory;
        private readonly InvoiceRepository _invoiceRepository;
        private readonly ApplicationDbContextFactory _context;
        private readonly UIInvoiceController _invoiceController;
        private readonly GhostDbContextFactory _dbContextFactory;
        private readonly PaymentRequestRepository _paymentRequestRepository;

        public GhostPluginService(
            AppService appService,
            EventAggregator eventAggregator,
            IHttpClientFactory clientFactory,
            ILogger<GhostPluginService> logger,
            InvoiceRepository invoiceRepository,
            ApplicationDbContextFactory context,
            UIInvoiceController invoiceController,
            GhostDbContextFactory dbContextFactory,
            PaymentRequestRepository paymentRequestRepository) : base(eventAggregator, logger)
        {
            _context = context;
            _appService = appService;
            _clientFactory = clientFactory;
            _dbContextFactory = dbContextFactory;
            _invoiceController = invoiceController;
            _invoiceRepository = invoiceRepository;
            _paymentRequestRepository = paymentRequestRepository;
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

        public async Task CreatePaymentRequestForActiveSubscriptionCloseToEnding()
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var tcs = new TaskCompletionSource<object>();

            PushEvent(new SequentialExecute(async () =>
            {
                var apps = await _appService.GetApps(GhostApp.AppType);
                apps = apps.Where(data => !data.Archived).ToList();
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
                                            TransactionStatus = Data.TransactionStatus.Pending,
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
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                        app.SetSettings(settings);
                        await _appService.UpdateOrCreateApp(app);
                    }
                }

                return null;
            }, tcs));
            await tcs.Task;
        }

        protected override void SubscribeToEvents()
        {
            Subscribe<PaymentRequestEvent>();
            Subscribe<SequentialExecute>();
            base.SubscribeToEvents();
        }

        public record SequentialExecute(Func<Task<object>> Action, TaskCompletionSource<object> TaskCompletionSource);


        public Dictionary<string, string> GetSupportedWebhookTypes()
        {
            throw new NotImplementedException();
        }

        public WebhookEvent CreateTestEvent(string type, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}

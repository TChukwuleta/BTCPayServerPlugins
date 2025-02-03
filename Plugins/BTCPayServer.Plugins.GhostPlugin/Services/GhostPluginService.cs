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

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostPluginService 
    {
        private readonly AppService _appService;
        private readonly InvoiceRepository _invoiceRepository;
        private readonly ApplicationDbContextFactory _context;
        private readonly UIInvoiceController _invoiceController;
        private readonly PaymentRequestRepository _paymentRequestRepository;

        public GhostPluginService(
            AppService appService,
            InvoiceRepository invoiceRepository,
            ApplicationDbContextFactory context,
            UIInvoiceController invoiceController,
            PaymentRequestRepository paymentRequestRepository)
        {
            _context = context;
            _appService = appService;
            _invoiceController = invoiceController;
            _invoiceRepository = invoiceRepository;
            _paymentRequestRepository = paymentRequestRepository;
        }

        public async Task<BTCPayServer.Data.PaymentRequestData> CreatePaymentRequest(GhostMember member, Tier tier, string appId, string url)
        {
            // Amount is in lower denomination, so divide by 100
            var price = member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price;
            var app = await _appService.GetApp(appId, GhostApp.AppType, false, false);
            var pr = new BTCPayServer.Data.PaymentRequestData()
            {
                StoreDataId = member.StoreId,
                Archived = false,
                Status = Client.Models.PaymentRequestData.PaymentRequestStatus.Pending
            };
            pr.SetBlob(new CreatePaymentRequestRequest()
            {
                StoreId = member.StoreId,
                Amount = (decimal)(price / 100),
                Currency = tier.currency,
                ExpiryDate = DateTimeOffset.UtcNow.AddDays(1),
                Description = $"{member.Name} Ghost membership renewal",
                Title = app.Name,
                Email = member.Email,
                AllowCustomPaymentAmounts = false,
                AdditionalData = new Dictionary<string, JToken>()
                {
                    {"source", JToken.FromObject("ghostsubscription")},
                    {"member", JToken.FromObject(member)},
                    {"tier", JToken.FromObject(tier)},
                    {"appId", JToken.FromObject(appId)},
                    {"url", url}
                }
            });
            pr = await _paymentRequestRepository.CreateOrUpdatePaymentRequest(pr);
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
    }
}

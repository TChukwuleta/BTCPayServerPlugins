using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using BTCPayServer.Services.Rates;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;
using BTCPayServer.Plugins.NairaCheckout;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Plugins.NairaCheckout.Services;
using BTCPayServer.Plugins.NairaCheckout.Data;
using NBitcoin.DataEncoders;
using NBitcoin;
using System.Net.Http;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.Template;

[Route("stores/{storeId}/naira")]
[Authorize(Policy = Policies.CanModifyServerSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
public class UINairaController : Controller
{
    private readonly RateFetcher _rateFactory;
    private readonly StoreRepository _storeRepository;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly PaymentMethodHandlerDictionary _handler;
    private readonly NairaStatusProvider _nairaStatusProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    public UINairaController
        (RateFetcher rateFactory,
        StoreRepository storeRepository,
        IHttpClientFactory clientFactory,
        InvoiceRepository invoiceRepository,
        PaymentMethodHandlerDictionary handler,
        NairaStatusProvider nairaStatusProvider,
        UserManager<ApplicationUser> userManager,
        NairaCheckoutDbContextFactory dbContextFactory)
    {
        _handler = handler;
        _userManager = userManager;
        _rateFactory = rateFactory;
        _clientFactory = clientFactory;
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _nairaStatusProvider = nairaStatusProvider;
    }
    private readonly List<string> lightningPaymentMethods = new List<string> { "BTC-LN" }; // "BTC-LNURL" Mavapay doeas not support LNURL yet
    private StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> StoreConfig()
    {
        var paymentMethods = StoreData.GetPaymentMethodConfigs(_handler, onlyEnabled: true);
        var hasLightningPaymentMethod = paymentMethods.Keys.Any(key => lightningPaymentMethods.Contains(key.ToString()));
        await using var ctx = _dbContextFactory.CreateContext();
        var existingSetting = ctx.MavapaySettings.FirstOrDefault(m => m.StoreId == StoreData.Id);
        var model = new NairaStoreViewModel { Enabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id), WebhookSecret = existingSetting?.WebhookSecret, ApiKey = existingSetting?.ApiKey };
        return View(model);
    }


    [HttpPost]
    public async Task<IActionResult> StoreConfig(NairaStoreViewModel viewModel)
    {
        var store = StoreData;
        var blob = StoreData.GetStoreBlob();
        var paymentMethodId = NairaCheckoutPlugin.NairaPmid;
        var paymentMethods = StoreData.GetPaymentMethodConfigs(_handler, onlyEnabled: true);
        var hasLightningPaymentMethod = paymentMethods.Keys.Any(key => lightningPaymentMethods.Contains(key.ToString()));
        if (!hasLightningPaymentMethod)
        {
            TempData[WellKnownTempData.ErrorMessage] = "You need to enable lightning payment to use this plugin";
            return RedirectToAction(nameof(StoreConfig), new { storeId = store.Id, paymentMethodId });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var apiClient = new MavapayApiClientService(_clientFactory, _dbContextFactory, _invoiceRepository);
        var webhookSecret = !string.IsNullOrEmpty(viewModel.WebhookSecret) ? viewModel.WebhookSecret : Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
        var url = Url.Action("ReceiveMavapayWebhook", "UINairaPublic", new { storeId = StoreData.Id }, Request.Scheme);
        var entity = ctx.NairaCheckoutSettings.FirstOrDefault(c => c.Enabled) ?? new NairaCheckoutSetting { WalletName = Wallet.Mavapay.ToString() };
        bool successfulCalls = false;
        if (viewModel.Enabled)
        {
            var existingSetting = ctx.MavapaySettings.FirstOrDefault(m => m.StoreId == StoreData.Id);
            bool needsUpdate = existingSetting == null || existingSetting.WebhookSecret != webhookSecret;
            bool webhookSuccess = !needsUpdate || await apiClient.UpdateWebhook(viewModel.ApiKey, url, webhookSecret);
            successfulCalls = webhookSuccess;
            if (webhookSuccess)
            {
                if (existingSetting == null)
                {
                    existingSetting = new MavapaySetting
                    {
                        ApiKey = viewModel.ApiKey,
                        WebhookSecret = webhookSecret,
                        StoreId = StoreData.Id,
                        StoreName = StoreData.StoreName,
                        ApplicationUserId = GetUserId(),
                        IntegratedAt = DateTime.UtcNow,
                    };
                    ctx.MavapaySettings.Add(existingSetting);
                }
                else
                {
                    existingSetting.ApiKey = viewModel.ApiKey;
                    existingSetting.WebhookSecret = webhookSecret;
                    ctx.MavapaySettings.Update(existingSetting);
                }
            }
        }
        if (!successfulCalls && viewModel.Enabled) 
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot connect to Mavapay. Please enter a valid Api Key";
            return RedirectToAction(nameof(StoreConfig), new { storeId = store.Id, paymentMethodId });
        }

        var currentPaymentMethodConfig = StoreData.GetPaymentMethodConfig<CashPaymentMethodConfig>(paymentMethodId, _handler);
        currentPaymentMethodConfig ??= new CashPaymentMethodConfig();
        blob.SetExcluded(paymentMethodId, !viewModel.Enabled);
        StoreData.SetPaymentMethodConfig(_handler[paymentMethodId], currentPaymentMethodConfig);
        store.SetStoreBlob(blob);
        await _storeRepository.UpdateStore(store);
        entity.Enabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id);
        ctx.NairaCheckoutSettings.Update(entity);
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(StoreConfig), new { storeId = store.Id, paymentMethodId });
    }

    private string GetUserId() => _userManager.GetUserId(User);

    private SelectList GetExchangesSelectList(string selected)
    {
        var exchanges = _rateFactory.RateProviderFactory
            .AvailableRateProviders
            .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var chosen = exchanges.Find(f => f.Id == selected) ?? exchanges[0];
        return new SelectList(exchanges, nameof(chosen.Id), nameof(chosen.DisplayName), chosen.Id);
    }
}

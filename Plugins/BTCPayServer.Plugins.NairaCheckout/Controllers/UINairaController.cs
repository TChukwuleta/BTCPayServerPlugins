using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.NairaCheckout;
using BTCPayServer.Plugins.NairaCheckout.Data;
using BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;
using BTCPayServer.Plugins.NairaCheckout.Services;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Template;

[Route("stores/{storeId}/naira")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UINairaController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly PaymentMethodHandlerDictionary _handler;
    private readonly NairaStatusProvider _nairaStatusProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    private readonly MavapayApiClientService _mavapayApiClientService;
    public UINairaController
        (StoreRepository storeRepository,
        IHttpClientFactory clientFactory,
        InvoiceRepository invoiceRepository,
        PaymentMethodHandlerDictionary handler,
        NairaStatusProvider nairaStatusProvider,
        UserManager<ApplicationUser> userManager,
        MavapayApiClientService mavapayApiClientService,
        NairaCheckoutDbContextFactory dbContextFactory)
    {
        _handler = handler;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _nairaStatusProvider = nairaStatusProvider;
        _mavapayApiClientService = mavapayApiClientService;
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


    [HttpGet("/mavapay/payout")]
    public async Task<IActionResult> MavapayPayout()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var nairaEnabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id);
        var entity = ctx.NairaCheckoutSettings.FirstOrDefault(c => c.Enabled);
        if (mavapaySetting == null || string.IsNullOrEmpty(mavapaySetting.ApiKey) || !nairaEnabled || entity.WalletName != Wallet.Mavapay.ToString())
        {
            TempData[WellKnownTempData.ErrorMessage] = "Kindly activate or configure Mavapay";
            return RedirectToAction(nameof(StoreConfig), new { storeId = StoreData.Id });
        }
        var ngnBanks = await _mavapayApiClientService.GetNGNBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            NGNBanks = ngnBanks.Select(s => new SelectListItem
            {
                Value = s.nipBankCode,
                Text = s.bankName
            }).ToList() ?? new List<SelectListItem>()
        };
        return View(viewModel);
    }

    [HttpPost("mavapay/ngn-payout")]
    public async Task<IActionResult> ProcessNGNPayout(MavapayPayoutViewModel model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var ngnBanks = await _mavapayApiClientService.GetNGNBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            NGN = model.NGN,
            KES = model.KES,
            ZAR = model.ZAR,
            NGNBanks = ngnBanks.Select(s => new SelectListItem
            {
                Value = s.nipBankCode,
                Text = s.bankName
            }).ToList() ?? new List<SelectListItem>()
        };
        if (string.IsNullOrWhiteSpace(model.NGN.AccountNumber) || model.NGN.AccountNumber.Length != 10)
        {
            ModelState.AddModelError("NGN.AccountNumber", "Account number must be exactly 10 digits");
            return View(nameof(MavapayPayout), viewModel);
        }
        var result = await _mavapayApiClientService.NGNNameEnquiry(model.NGN.BankCode, model.NGN.AccountNumber, mavapaySetting.ApiKey);
        if (result == null || string.IsNullOrEmpty(result.accountName))
        {
            ModelState.AddModelError("NGN.AccountNumber", "Account number cannot be verified at the moment");
            return View(nameof(MavapayPayout), viewModel);
        }
        viewModel.NGN.AccountName = result.accountName;
        return View(nameof(MavapayPayout), viewModel);
    }

    [HttpPost("mavapay/name-enquiry")]
    public async Task<IActionResult> ValidateNgnAccountNumber(MavapayPayoutViewModel model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var ngnBanks = await _mavapayApiClientService.GetNGNBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            NGN = model.NGN,
            KES = model.KES,
            ZAR = model.ZAR,
            NGNBanks = ngnBanks.Select(s => new SelectListItem
            {
                Value = s.nipBankCode,
                Text = s.bankName
            }).ToList() ?? new List<SelectListItem>()
        };
        if (string.IsNullOrWhiteSpace(model.NGN.AccountNumber) || model.NGN.AccountNumber.Length != 10)
        {
            ModelState.AddModelError("NGN.AccountNumber", "Account number must be exactly 10 digits");
            return View(nameof(MavapayPayout), viewModel);
        }
        var result = await _mavapayApiClientService.NGNNameEnquiry(model.NGN.BankCode, model.NGN.AccountNumber, mavapaySetting.ApiKey);
        if (result == null || string.IsNullOrEmpty(result.accountName))
        {
            ModelState.AddModelError("NGN.AccountNumber", "Account number cannot be verified at the moment");
            return View(nameof(MavapayPayout), viewModel);
        }
        viewModel.NGN.AccountName = result.accountName;
        return View(nameof(MavapayPayout), viewModel);
    }


    private string GetUserId() => _userManager.GetUserId(User);
}

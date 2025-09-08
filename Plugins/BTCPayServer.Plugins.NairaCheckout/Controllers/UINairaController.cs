using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Data.Payouts.LightningLike;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Payouts;
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
using static BTCPayServer.Models.WalletViewModels.PayoutsModel;

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
    private readonly PullPaymentHostedService _pullPaymentService;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    private readonly MavapayApiClientService _mavapayApiClientService;
    public UINairaController
        (StoreRepository storeRepository,
        IHttpClientFactory clientFactory,
        InvoiceRepository invoiceRepository,
        PaymentMethodHandlerDictionary handler,
        NairaStatusProvider nairaStatusProvider,
        UserManager<ApplicationUser> userManager,
        PullPaymentHostedService pullPaymentService,
        MavapayApiClientService mavapayApiClientService,
        NairaCheckoutDbContextFactory dbContextFactory)
    {
        _handler = handler;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _pullPaymentService = pullPaymentService;
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
        var zarBanks = await _mavapayApiClientService.GetZARBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            KESPaymentMethod = Enum.GetValues(typeof(MpesaPaymentMethod)).Cast<MpesaPaymentMethod>().Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = System.Text.RegularExpressions.Regex.Replace(s.ToString(), "([a-z])([A-Z])", "$1 $2")
            }).ToList(),
            ZARBanks = zarBanks.Select(s => new SelectListItem
            {
                Value = s,
                Text = s
            }).ToList() ?? new List<SelectListItem>(),
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
        if (string.IsNullOrWhiteSpace(model.NGN.AccountNumber) || model.NGN.AccountNumber.Length != 10)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Account number must be exactly 10 digits";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        var result = await _mavapayApiClientService.NGNNameEnquiry(model.NGN.BankCode, model.NGN.AccountNumber, mavapaySetting.ApiKey);
        if (result == null || string.IsNullOrEmpty(result.accountName))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Account number cannot be verified at the moment";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        model.NGN.AccountName = result.accountName;
        model.NGN.BankName = model.NGN.BankCode;
        try
        {
            var ngnPayout = await _mavapayApiClientService.MavapayNairaPayout(model.NGN, mavapaySetting.ApiKey);
            if (!string.IsNullOrEmpty(ngnPayout.ErrorMessage))
            {
                TempData[WellKnownTempData.ErrorMessage] = ngnPayout.ErrorMessage;
                return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
            }
            await ClaimPayout(ctx, ngnPayout, StoreData.Id, SupportedCurrency.NGN.ToString(), model.NGN.AccountNumber);
            TempData[WellKnownTempData.SuccessMessage] = "Pauyout processed successfully";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Error processing NGN payout - {ex.Message}";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
    }

    [HttpPost("mavapay/naira/name-enquiry")]
    public async Task<IActionResult> ValidateNgnAccountNumber(MavapayPayoutViewModel model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var ngnBanks = await _mavapayApiClientService.GetNGNBanks(mavapaySetting.ApiKey);
        var zarBanks = await _mavapayApiClientService.GetZARBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            NGN = model.NGN,
            KES = model.KES,
            ZAR = model.ZAR,
            KESPaymentMethod = Enum.GetValues(typeof(MpesaPaymentMethod)).Cast<MpesaPaymentMethod>().Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = System.Text.RegularExpressions.Regex.Replace(s.ToString(), "([a-z])([A-Z])", "$1 $2")
            }).ToList(),
            ZARBanks = zarBanks.Select(s => new SelectListItem
            {
                Value = s,
                Text = s
            }).ToList() ?? new List<SelectListItem>(),
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


    [HttpPost("mavapay/zar-payout")]
    public async Task<IActionResult> ProcessZARPayout(MavapayPayoutViewModel model)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        if (string.IsNullOrWhiteSpace(model.ZAR.AccountNumber) || string.IsNullOrWhiteSpace(model.ZAR.AccountName) || model.ZAR.Amount <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please enter valid account details and amount";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        try
        {
            var zarPayout = await _mavapayApiClientService.MavapayRandsPayout(model.ZAR, mavapaySetting.ApiKey);
            if (!string.IsNullOrEmpty(zarPayout.ErrorMessage))
            {
                TempData[WellKnownTempData.ErrorMessage] = zarPayout.ErrorMessage;
                return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
            }
            await ClaimPayout(ctx, zarPayout, StoreData.Id, SupportedCurrency.ZAR.ToString(), model.ZAR.AccountNumber);
            TempData[WellKnownTempData.SuccessMessage] = "Pauyout processed successfully";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Error processing ZAR payout - {ex.Message}";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
    }

    [HttpPost("mavapay/kes-payout")]
    public async Task<IActionResult> ProcessKESPayout(MavapayPayoutViewModel model)
    {
        Console.WriteLine(JsonConvert.SerializeObject(model.KES));
        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        if (string.IsNullOrWhiteSpace(model.KES.Identifier) || model.KES.Amount <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please enter valid account details and amount";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        try
        {
            var kesPayout = await _mavapayApiClientService.MavapayKenyanShillingPayout(model.KES, mavapaySetting.ApiKey);
            if (!string.IsNullOrEmpty(kesPayout.ErrorMessage))
            {
                TempData[WellKnownTempData.ErrorMessage] = kesPayout.ErrorMessage;
                return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
            }
            await ClaimPayout(ctx, kesPayout, StoreData.Id, SupportedCurrency.KES.ToString(), model.KES.Identifier);
            TempData[WellKnownTempData.SuccessMessage] = "Pauyout processed successfully";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Error processing ZAR payout - {ex.Message}";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
    }


    [HttpGet("/mavapay/transactions/{externalReferemce}/status")]
    public async Task<IActionResult> VerifyMavapayTransaction(string storeId, string externalReferemce)
    {
        // Do something
        var verifyTransaction = await _mavapayApiClientService.GetMavapayTransactionRecord(externalReferemce, storeId);

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
        var zarBanks = await _mavapayApiClientService.GetZARBanks(mavapaySetting.ApiKey);
        var viewModel = new MavapayPayoutViewModel
        {
            KESPaymentMethod = Enum.GetValues(typeof(MpesaPaymentMethod)).Cast<MpesaPaymentMethod>().Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = System.Text.RegularExpressions.Regex.Replace(s.ToString(), "([a-z])([A-Z])", "$1 $2")
            }).ToList(),
            ZARBanks = zarBanks.Select(s => new SelectListItem
            {
                Value = s,
                Text = s
            }).ToList() ?? new List<SelectListItem>(),
            NGNBanks = ngnBanks.Select(s => new SelectListItem
            {
                Value = s.nipBankCode,
                Text = s.bankName
            }).ToList() ?? new List<SelectListItem>()
        };
        return View(viewModel);
    }

    private async Task ClaimPayout(NairaCheckoutDbContext ctx, CreatePayoutResponseModel responseModel, string storeId, string currency, string accountNumber)
    {
        TimeSpan expirySpan = responseModel.expiry - DateTime.UtcNow;
        var pullPaymentId = await _pullPaymentService.CreatePullPayment(StoreData, new Client.Models.CreatePullPaymentRequest
        {
            Name = $"Mavapay {currency} Payout - {accountNumber}",
            Amount = responseModel.amountInSourceCurrency,
            Currency = "SATS",
            BOLT11Expiration = expirySpan,
            PayoutMethods = new[]
            {
                PaymentTypes.CHAIN.GetPaymentMethodId("BTC").ToString(),
                PaymentTypes.LN.GetPaymentMethodId("BTC").ToString()
            },
            AutoApproveClaims = true,
        });

        ctx.PayoutTransactions.Add(new PayoutTransaction
        {
            Provider = Wallet.Mavapay.ToString(),
            Amount = responseModel.amountInSourceCurrency,
            PullPaymentId = pullPaymentId,
            BaseCurrency = currency, 
            Currency = "SATS",
            Identifier = accountNumber,
            StoreId = storeId,
            ExternalReference = responseModel.id,
            CreatedAt = DateTimeOffset.UtcNow,
            Data = JsonConvert.SerializeObject(responseModel),
        });
        await ctx.SaveChangesAsync();

        await _pullPaymentService.Claim(new ClaimRequest()
        {
            Destination = new LNURLPayClaimDestinaton(responseModel.invoice),
            PullPaymentId = pullPaymentId,
            ClaimedAmount = responseModel.amountInSourceCurrency,
            PayoutMethodId = PayoutTypes.LN.GetPayoutMethodId("BTC"),
            StoreId = StoreData.Id,
        });
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

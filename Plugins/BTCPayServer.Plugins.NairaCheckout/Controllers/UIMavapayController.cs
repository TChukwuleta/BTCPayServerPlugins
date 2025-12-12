using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
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

namespace BTCPayServer.Plugins.Template;

[Route("stores/{storeId}/mavapay/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIMavapayController : Controller
{
    private readonly StoreRepository _storeRepository;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly PaymentMethodHandlerDictionary _handler;
    private readonly NairaStatusProvider _nairaStatusProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PullPaymentHostedService _pullPaymentService;
    private readonly GeneralCheckoutService _generalCheckoutService;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    private readonly MavapayApiClientService _mavapayApiClientService;
    public UIMavapayController
        (StoreRepository storeRepository,
        IHttpClientFactory clientFactory,
        InvoiceRepository invoiceRepository,
        PaymentMethodHandlerDictionary handler,
        NairaStatusProvider nairaStatusProvider,
        UserManager<ApplicationUser> userManager,
        PullPaymentHostedService pullPaymentService,
        GeneralCheckoutService generalCheckoutService,
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
        _generalCheckoutService = generalCheckoutService;
        _mavapayApiClientService = mavapayApiClientService;
    }

    private readonly List<string> lightningPaymentMethods = new List<string> { "BTC-LN" }; // "BTC-LNURL" Mavapay doeas not support LNURL yet
    private StoreData StoreData => HttpContext.GetStoreData();

    [HttpGet]
    public async Task<IActionResult> StoreConfig()
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

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
        var paymentMethodId = NairaCheckoutPlugin.NairaPmid;
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        if (!StoreData.GetPaymentMethodConfigs(_handler, onlyEnabled: true).Keys.Any(k => lightningPaymentMethods.Contains(k.ToString())))
        {
            TempData[WellKnownTempData.ErrorMessage] = "You need to enable lightning payment (BTC-LN) to use this plugin";
            return RedirectToAction(nameof(StoreConfig), new { StoreData.Id, paymentMethodId });
        }

        var blob = StoreData.GetStoreBlob();
        var config = StoreData.GetPaymentMethodConfig<CashPaymentMethodConfig>(paymentMethodId, _handler) ?? new CashPaymentMethodConfig();
        var webhookSecret = !string.IsNullOrEmpty(viewModel.WebhookSecret) ? viewModel.WebhookSecret : Encoders.Base58.EncodeData(RandomUtils.GetBytes(10));
        var webhookUrl = Url.Action(nameof(UIMavapayPublicController.ReceiveMavapayWebhook), "UIMavapayPublic", new { storeId = StoreData.Id }, Request.Scheme);

        await using var ctx = _dbContextFactory.CreateContext();
        var apiClient = new MavapayApiClientService(_clientFactory, _dbContextFactory, _invoiceRepository, _pullPaymentService);
        var entity = ctx.NairaCheckoutSettings.FirstOrDefault(c => c.Enabled) ?? new NairaCheckoutSetting { WalletName = Wallet.Mavapay.ToString() };

        bool webhookOk = true;
        if (viewModel.Enabled)
        {
            var existing = ctx.MavapaySettings.FirstOrDefault(m => m.StoreId == StoreData.Id);
            bool needsUpdate = existing == null || existing.WebhookSecret != webhookSecret;
            if (needsUpdate)
                webhookOk = await apiClient.UpdateWebhook(viewModel.ApiKey, webhookUrl, webhookSecret);

            if (!webhookOk)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Cannot connect to Mavapay. Please enter a valid Api Key";
                return RedirectToAction(nameof(StoreConfig), new { StoreData.Id, paymentMethodId });
            }
            if (existing == null)
            {
                ctx.MavapaySettings.Add(new MavapaySetting
                {
                    ApiKey = viewModel.ApiKey,
                    WebhookSecret = webhookSecret,
                    StoreId = StoreData.Id,
                    StoreName = StoreData.StoreName,
                    ApplicationUserId = GetUserId(),
                    IntegratedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.ApiKey = viewModel.ApiKey;
                existing.WebhookSecret = webhookSecret;
                ctx.MavapaySettings.Update(existing);
            }
        }

        blob.SetExcluded(paymentMethodId, !viewModel.Enabled);
        StoreData.SetPaymentMethodConfig(_handler[paymentMethodId], config);
        StoreData.SetStoreBlob(blob);
        await _storeRepository.UpdateStore(StoreData);

        var checkout = ctx.NairaCheckoutSettings.FirstOrDefault(c => c.Enabled) ?? new NairaCheckoutSetting { WalletName = Wallet.Mavapay.ToString() };
        checkout.Enabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id);

        if (string.IsNullOrEmpty(checkout.Id))
            ctx.NairaCheckoutSettings.Add(checkout);
        else
            ctx.NairaCheckoutSettings.Update(checkout);

        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(StoreConfig), new { StoreData.Id, paymentMethodId });
    }


    [HttpGet("payout")]
    public async Task<IActionResult> MavapayPayout()
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var nairaEnabled = await _nairaStatusProvider.NairaEnabled(StoreData.Id);
        var entity = ctx.NairaCheckoutSettings.FirstOrDefault(c => c.Enabled);
        if (mavapaySetting == null || string.IsNullOrEmpty(mavapaySetting.ApiKey) || !nairaEnabled || entity.WalletName != Wallet.Mavapay.ToString())
        {
            TempData[WellKnownTempData.ErrorMessage] = "Kindly activate or configure Mavapay";
            return RedirectToAction(nameof(StoreConfig), new { storeId = StoreData.Id });
        }
        var viewModel = await PayoutViewModel(mavapaySetting);
        return View(viewModel);
    }

    [HttpPost("naira/name-enquiry")]
    public async Task<IActionResult> ValidateNgnAccountNumber(MavapayPayoutViewModel model)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var viewModel = await PayoutViewModel(mavapaySetting, model);
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

    [HttpPost("ngn-payout")]
    public async Task<IActionResult> ProcessNGNPayout(MavapayPayoutViewModel model)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

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
            var lightningBalance = await GetLightningBalance(StoreData.Id);
            if (lightningBalance <= ngnPayout.totalAmountInSourceCurrency)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Insufficient balance to process request";
                return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
            }
            await _mavapayApiClientService.ClaimPayout(ctx, ngnPayout, StoreData, SupportedCurrency.NGN.ToString(), model.NGN.AccountNumber);
            TempData[WellKnownTempData.SuccessMessage] = "Pauyout processed successfully";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Error processing NGN payout - {ex.Message}";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
    }

    [HttpPost("zar-payout")]
    public async Task<IActionResult> ProcessZARPayout(MavapayPayoutViewModel model)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

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
            var lightningBalance = await GetLightningBalance(StoreData.Id);
            if (lightningBalance <= zarPayout.totalAmountInSourceCurrency)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Insufficient balance to process request";
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

    [HttpPost("kes/name-enquiry")]
    public async Task<IActionResult> ValidateKesTillAndBillNumber(MavapayPayoutViewModel model)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        var viewModel = await PayoutViewModel(mavapaySetting, model);

        if (string.IsNullOrWhiteSpace(model.KES.Identifier))
        {
            ModelState.AddModelError("KES.Identifier", "Identifier number must be provided");
            return View(nameof(MavapayPayout), viewModel);
        }
        var result = await _mavapayApiClientService.KESNameEnquiry(model.KES.Identifier, model.KES.Method, mavapaySetting.ApiKey);
        if (result == null || string.IsNullOrEmpty(result?.organization_name))
        {
            ModelState.AddModelError("KES.Identifier", "Till or Bill number cannot be verified at the moment");
            return View(nameof(MavapayPayout), viewModel);
        }
        viewModel.KES.AccountName = result.organization_name;
        return View(nameof(MavapayPayout), viewModel);
    }

    [HttpPost("kes-payout")]
    public async Task<IActionResult> ProcessKESPayout(MavapayPayoutViewModel model)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

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
            var lightningBalance = await GetLightningBalance(StoreData.Id);
            if (lightningBalance <= kesPayout.totalAmountInSourceCurrency)
            {
                TempData[WellKnownTempData.ErrorMessage] = "Insufficient balance to process request";
                return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
            }
            await ClaimPayout(ctx, kesPayout, StoreData.Id, SupportedCurrency.KES.ToString(), model.KES.Identifier);
            TempData[WellKnownTempData.SuccessMessage] = "Pauyout processed successfully";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"Error processing KES payout - {ex.Message}";
            return RedirectToAction(nameof(MavapayPayout), new { storeId = StoreData.Id });
        }
    }

    [HttpGet("mavapay-checkout-settings")]
    public async Task<IActionResult> MavapayCheckoutSettings(string storeId)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        if (mavapaySetting == null)
            return NotFound();

        var settings = await _storeRepository.GetSettingAsync<MavapayCheckoutSettings>(storeId, NairaCheckoutPlugin.SettingsName) ?? new MavapayCheckoutSettings();
        var viewModel = await PayoutViewModel(mavapaySetting);
        var model = new SplitPaymentSettingsViewModel
        {
            SplitPercentage = settings.SplitPercentage,
            NGNBankCode = settings.NGNBankCode,
            NGNAccountNumber = settings.NGNAccountNumber,
            NGNAccountName = settings.NGNAccountName,
            NGNBankName = settings.NGNBankName,
            KESMethod = settings.KESMethod,
            KESIdentifier = settings.KESIdentifier,
            KESAccountName = settings.KESAccountName,
            KESAccountNumber = settings.KESAccountNumber,
            ZARBank = settings.ZARBank,
            ZARAccountNumber = settings.ZARAccountNumber,
            ZARAccountName = settings.ZARAccountName,
            KESPaymentMethod = viewModel.KESPaymentMethod,
            ZARBanks = viewModel.ZARBanks,
            NGNBanks = viewModel.NGNBanks,
            Currency = !string.IsNullOrEmpty(settings.Currency) ? settings.Currency : SupportedCurrency.NGN.ToString()
        };
        return View(new MavapaySettingViewModel { EnableSplitPayment = settings.EnableSplitPayment, SplitPayment = model });
    }

    [HttpPost("mavapay-checkout-settings")]
    public async Task<IActionResult> MavapayCheckoutSettings(MavapaySettingViewModel vm)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        if (vm.SplitPayment.SplitPercentage is < 0 or > 100)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Please enter a valid percentage value";
            return RedirectToAction(nameof(MavapayCheckoutSettings), new { storeId = StoreData.Id });
        }

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        if (mavapaySetting == null)
            return NotFound();

        var settings = await _storeRepository.GetSettingAsync<MavapayCheckoutSettings>(StoreData.Id, NairaCheckoutPlugin.SettingsName) ?? new MavapayCheckoutSettings();
        settings = new MavapayCheckoutSettings
        {
            EnableSplitPayment = vm.EnableSplitPayment,
            SplitPercentage = vm.SplitPayment.SplitPercentage,
            Currency = vm.SplitPayment.Currency.ToString(),
            NGNBankCode = vm.SplitPayment.NGNBankCode,
            NGNAccountNumber = vm.SplitPayment.NGNAccountNumber,
            NGNAccountName = vm.SplitPayment.NGNAccountName,
            NGNBankName = vm.SplitPayment.NGNBankName,
            KESMethod = vm.SplitPayment.KESMethod,
            KESIdentifier = vm.SplitPayment.KESIdentifier,
            KESAccountNumber = vm.SplitPayment.KESAccountNumber,
            KESAccountName = vm.SplitPayment.KESAccountName,
            ZARBank = vm.SplitPayment.ZARBank,
            ZARAccountNumber = vm.SplitPayment.ZARAccountNumber,
            ZARAccountName = vm.SplitPayment.ZARAccountName
        };
        await _storeRepository.UpdateSetting(StoreData.Id, NairaCheckoutPlugin.SettingsName, settings);
        TempData[WellKnownTempData.SuccessMessage] = "Mavapay checkout settings saved successfully";
        return RedirectToAction(nameof(MavapayCheckoutSettings), new { storeId = StoreData.Id });
    }

    [HttpGet("mavapay/payout/list")]
    public async Task<IActionResult> ListMavapayPayouts(string storeId, string searchText)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var payoutTransactions = ctx.PayoutTransactions.Where(c => c.StoreId == StoreData.Id && c.Provider == Wallet.Mavapay.ToString());
        if (!string.IsNullOrEmpty(searchText))
        {
            payoutTransactions = payoutTransactions.Where(o => o.ExternalReference.Contains(searchText) || o.PullPaymentId.Contains(searchText));
        }
        List<PayoutTransactionVm> vm = payoutTransactions.ToList().Select(c => new PayoutTransactionVm
        {
            Currency = c.Currency,
            Amount = c.Amount,
            ExternalReference = GetExternalReferenceId(c.ExternalReference),
            PullPaymentId = c.PullPaymentId,
            IsSuccess = c.IsSuccess,
            CompletedAt = c.CompletedAt
        }).ToList();
        return View(new PayoutListViewModel { PayoutTransactions = vm, SearchText = searchText });
    }

    [HttpGet("/mavapay/transactions/{externalReferemce}/status")]
    public async Task<IActionResult> VerifyMavapayTransaction(string storeId, string externalReferemce)
    {
        if (string.IsNullOrEmpty(StoreData.Id))
            return NotFound();

        await using var ctx = _dbContextFactory.CreateContext();
        var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == StoreData.Id);
        if (mavapaySetting == null || string.IsNullOrEmpty(mavapaySetting.ApiKey))
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot verify transaction. Kindly enable your mavapay account";
            return RedirectToAction(nameof(ListMavapayPayouts), new { storeId = StoreData.Id });
        }
        var transactionRecord = ctx.PayoutTransactions.FirstOrDefault(c => c.StoreId == StoreData.Id && c.ExternalReference.EndsWith(":" + externalReferemce) && c.Provider == Wallet.Mavapay.ToString());
        if (mavapaySetting == null)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Cannot find reference transaction";
            return RedirectToAction(nameof(ListMavapayPayouts), new { storeId = StoreData.Id });
        }
        var verifiedTransaction = await _mavapayApiClientService.GetMavapayTransactionRecord(mavapaySetting.ApiKey, hash: externalReferemce);
        var successfulTransaction = verifiedTransaction?.All(c => c.status.Equals("SUCCESS", StringComparison.InvariantCultureIgnoreCase)) ?? false;
        if (successfulTransaction)
        {
            await _mavapayApiClientService.MarkTransactionStatusAsSuccess(ctx, externalReferemce, StoreData.Id);
        }
        TempData[WellKnownTempData.SuccessMessage] = successfulTransaction ? "Transaction validated and completed successfully" : "Transaction queried successfully. Transaction pending completion from Mavapay";
        return RedirectToAction(nameof(ListMavapayPayouts), new { storeId = StoreData.Id });
    }

    private async Task ClaimPayout(NairaCheckoutDbContext ctx, CreatePayoutResponseModel responseModel, string storeId, string currency, string accountNumber)
    {
        TimeSpan expirySpan = responseModel.expiry - DateTime.UtcNow;
        var pullPaymentId = await _pullPaymentService.CreatePullPayment(StoreData, new Client.Models.CreatePullPaymentRequest
        {
            Name = $"Mavapay {currency} Payout - {accountNumber}",
            Amount = responseModel.totalAmountInSourceCurrency,
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
            Amount = responseModel.totalAmountInSourceCurrency,
            PullPaymentId = pullPaymentId,
            BaseCurrency = currency, 
            Currency = "SATS",
            Identifier = accountNumber,
            StoreId = storeId,
            ExternalReference = $"{responseModel.id}:{responseModel.hash}",
            CreatedAt = DateTimeOffset.UtcNow,
            Data = JsonConvert.SerializeObject(responseModel),
        });
        await ctx.SaveChangesAsync();

        await _pullPaymentService.Claim(new ClaimRequest()
        {
            Destination = new LNURLPayClaimDestinaton(responseModel.invoice),
            PullPaymentId = pullPaymentId,
            ClaimedAmount = responseModel.totalAmountInSourceCurrency,
            PayoutMethodId = PayoutTypes.LN.GetPayoutMethodId("BTC"),
            StoreId = StoreData.Id,
        });
    }

    private async Task<MavapayPayoutViewModel> PayoutViewModel(MavapaySetting mavapaySetting, MavapayPayoutViewModel model = null)
    {
        var ngnBanks = await _mavapayApiClientService.GetNGNBanks(mavapaySetting.ApiKey);
        var zarBanks = await _mavapayApiClientService.GetZARBanks(mavapaySetting.ApiKey);
        var vm = new MavapayPayoutViewModel
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
        if (model != null)
        {
            vm.NGN = model.NGN;
            vm.KES = model.KES;
            vm.ZAR = model.ZAR;
        }
        return vm;
    }

    private string GetUserId() => _userManager.GetUserId(User);

    private string GetExternalReferenceId(string reference) => reference.Split(':', 2)[1];

    private async Task<long> GetLightningBalance(string storeId)
    {
        var balance = await _generalCheckoutService.GetLightningNodeBalance(storeId);
        return balance.MilliSatoshi / 1000;
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Controllers;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using Microsoft.Extensions.Logging;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Invoices;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using BTCPayServer.Payments;
using System.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Services;
using BTCPayServer.Plugins.Salesforce.Services;
using BTCPayServer.Plugins.Salesforce.Data;

namespace BTCPayServer.Plugins.Salesforce;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UISalesforceController : Controller
{
    private readonly string[] _keywords = new[] { "bitcoin", "btc", "btcpayserver", "btcpay server" };
    private readonly SalesforceHostedService _shopifyService;
    private readonly ILogger<UISalesforceController> _logger;
    private readonly StoreRepository _storeRepo;
    private readonly UriResolver _uriResolver;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly UIInvoiceController _invoiceController;
    private readonly ApplicationDbContextFactory _context;
    private readonly SalesforceDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly IHttpClientFactory _clientFactory;
    public UISalesforceController
        (UriResolver uriResolver,
        StoreRepository storeRepo,
        BTCPayNetworkProvider networkProvider,
        UIInvoiceController invoiceController,
        UserManager<ApplicationUser> userManager,
        SalesforceHostedService shopifyService,
        ApplicationDbContextFactory context,
        SalesforceDbContextFactory dbContextFactory,
        InvoiceRepository invoiceRepository,
        IHttpClientFactory clientFactory,
        ILogger<UISalesforceController> logger)
    {
        _context = context;
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _shopifyService = shopifyService;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
        _logger = logger;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [Route("~/plugins/stores/{storeId}/salesforce")]
    public async Task<IActionResult> Index(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var storeHasWallet = GetPaymentMethodConfigs(storeData, true).Any();
        if (!storeHasWallet)
        {
            return View(new SalesforceSetting
            {
                CryptoCode = _networkProvider.DefaultNetwork.CryptoCode,
                StoreId = storeId,
                HasWallet = false
            });
        }
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.SalesforceSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id) ?? new SalesforceSetting();
        var apiClient = new SalesforceApiClient(_clientFactory);
        var auth = await apiClient.Authenticate(userStore);
        await apiClient.CreateAlternativePaymentMethod(auth, userStore, "012gL000001P4JBQA0");
        return View(userStore);

        
    }


    [HttpPost("~/plugins/stores/{storeId}/salesforce")]
    public async Task<IActionResult> Index(string storeId,
            SalesforceSetting vm, string command = "")
    {
        try 
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var salesforceSetting = ctx.SalesforceSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == CurrentStore.Id);
            switch (command)
            {
                case "SaveSalseforcePaymentGatewayProvider":
                    {
                        var validCreds = vm?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Salesforce credentials";
                            return View(vm);
                        }
                        try
                        {
                            var request = HttpContext.Request;
                            string baseUrl = $"{request.Scheme}://{request.Host}".TrimEnd('/');
                            var apiClient = new SalesforceApiClient(_clientFactory);
                            var paymentGatewayId = await apiClient.FetchPaymentGatewayProviderId(vm);
                            await apiClient.SetupCustomObject(vm, baseUrl, storeId);
                            vm.PaymentGatewayProvider = paymentGatewayId;
                        }
                        catch (SalesforceApiException err)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = $"Unable to retrieve payment gateway: {err.Message}";
                            return View(vm);
                        }
                        ctx.Update(vm);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Salesforce payment gateway saved successfully";
                        break;
                    }
                case "SalseforceSaveCredentials":
                    {
                        var validCreds = vm?.CredentialsPopulated() == true;
                        if (!validCreds)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = "Please provide valid Salesforce credentials";
                            return View(vm);
                        }
                        try
                        {
                            var apiClient = new SalesforceApiClient(_clientFactory);
                            var request = HttpContext.Request;
                            string baseUrl = $"{request.Scheme}://{request.Host}".TrimEnd('/');
                            await apiClient.SetupCustomObject(vm, baseUrl, storeId);
                            var paymentGatewayId = await apiClient.FetchPaymentGatewayProviderId(vm);
                            vm.PaymentGatewayProvider = paymentGatewayId;
                        }
                        catch (SalesforceApiException err)
                        {
                            TempData[WellKnownTempData.ErrorMessage] = $"Invalid Salesforce credentials: {err.Message}";
                            return View(vm);
                        }
                        vm.IntegratedAt = DateTimeOffset.UtcNow;
                        vm.StoreId = CurrentStore.Id;
                        vm.ApplicationUserId = GetUserId();
                        vm.StoreName = CurrentStore.StoreName;
                        ctx.Update(vm);
                        await ctx.SaveChangesAsync();
                        TempData[WellKnownTempData.SuccessMessage] = "Salesforce plugin successfully updated";
                        break;
                    }
                case "SalesforceClearCredentials":
                    {
                        if (salesforceSetting != null)
                        {
                            ctx.Remove(salesforceSetting);
                            await ctx.SaveChangesAsync();
                        }
                        TempData[WellKnownTempData.SuccessMessage] = "Salesforce plugin credentials cleared";
                        break;
                    }
            }
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
        catch (Exception ex)
        {
            TempData[WellKnownTempData.ErrorMessage] = $"An error occurred on salesforce plugin. {ex.Message}";
            return RedirectToAction(nameof(Index), new { storeId = CurrentStore.Id });
        }
    }

    private static bool VerifyWebhookSignature(string requestBody, string shopifyHmacHeader, string clientSecret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(clientSecret);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
            var hashString = Convert.ToBase64String(hashBytes);
            return hashString.Equals(shopifyHmacHeader, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<PaymentMethodId, JToken> GetPaymentMethodConfigs(StoreData storeData, bool onlyEnabled = false)
    {
        if (string.IsNullOrEmpty(storeData.DerivationStrategies))
            return new Dictionary<PaymentMethodId, JToken>();
        var excludeFilter = onlyEnabled ? storeData.GetStoreBlob().GetExcludedPaymentMethods() : null;
        var paymentMethodConfigurations = new Dictionary<PaymentMethodId, JToken>();
        JObject strategies = JObject.Parse(storeData.DerivationStrategies);
        foreach (var strat in strategies.Properties())
        {
            if (!PaymentMethodId.TryParse(strat.Name, out var paymentMethodId))
                continue;
            if (excludeFilter?.Match(paymentMethodId) is true)
                continue;
            paymentMethodConfigurations.Add(paymentMethodId, strat.Value);
        }
        return paymentMethodConfigurations;
    }

    private string GetUserId() => _userManager.GetUserId(User);
}
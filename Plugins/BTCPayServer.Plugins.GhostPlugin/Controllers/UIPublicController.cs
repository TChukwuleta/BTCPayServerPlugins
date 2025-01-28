using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Services.Stores;
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Models;
using BTCPayServer.Services;
using BTCPayServer.Plugins.GhostPlugin.Data;
using System.Collections.Generic;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Controllers;
using BTCPayServer.Abstractions.Extensions;
using Newtonsoft.Json.Linq;
using System.Globalization;
using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Cors;
using BTCPayServer.Plugins.GhostPlugin.ViewModels;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[AllowAnonymous]
[Route("~/plugins/{storeId}/ghost/public/")]
public class UIPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly StoreRepository _storeRepo;
    private readonly IHttpClientFactory _clientFactory;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly ApplicationDbContextFactory _context;
    private readonly UIInvoiceController _invoiceController;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private GhostHelper helper;
    public UIPublicController
        (UriResolver uriResolver,
        StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        ApplicationDbContextFactory context,
        InvoiceRepository invoiceRepository,
        BTCPayNetworkProvider networkProvider,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        helper = new GhostHelper();
        _context = context;
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _invoiceController = invoiceController;
    }

    private const string GHOST_MEMBER_ID_PREFIX = "Ghost_member-";


    [HttpGet("create-member")]
    public async Task<IActionResult> CreateMember(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();

        var storeData = await _storeRepo.FindStore(storeId);

        return View(new CreateMemberViewModel { 
            GhostTiers = ghostTiers, 
            StoreId = storeId, 
            StoreName = storeData?.StoreName, 
            ShopName = ghostSetting.ApiUrl,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, storeData?.GetStoreBlob()),
        });
    }


    [HttpPost("create-member")]
    public async Task<IActionResult> CreateMember(CreateMemberViewModel vm, string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (ghostSetting == null || !ghostSetting.CredentialsPopulated())
            return NotFound();

        var storeData = await _storeRepo.FindStore(storeId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        Tier tier = ghostTiers.FirstOrDefault(c => c.id == vm.TierId);
        if (tier == null)
            return NotFound();

        GhostMember entity = new GhostMember
        {
            CreatedAt = DateTime.UtcNow,
            Name = vm.Name,
            Email = vm.Email,
            Frequency = vm.TierSubscriptionFrequency,
            TierId = vm.TierId,
            StoreId = storeId
        };
        ctx.Add(entity);
        await ctx.SaveChangesAsync();
        InvoiceEntity invoice = await CreateInvoiceAsync(storeData, tier, entity);
        GhostTransaction transaction = new GhostTransaction
        {
            StoreId = storeId,
            InvoiceId = invoice.Id,
            MemberId = entity.Id,
            TransactionStatus = GhostPlugin.Data.TransactionStatus.Pending,
            TierId = vm.TierId,
            Frequency = vm.TierSubscriptionFrequency,
            CreatedAt = DateTime.UtcNow,
            Amount = (decimal)(vm.TierSubscriptionFrequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price)
        };
        ctx.Add(transaction);
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(InitiatePayment), new { memberId = entity.Id, invoiceId = invoice.Id });
    }


    [HttpGet("initiate-payment/{memberId}/{invoiceId}")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> InitiatePayment(string memberId, string invoiceId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostMember = ctx.GhostMembers.AsNoTracking().FirstOrDefault(c => c.Id == memberId);

        await using var dbMain = _context.CreateContext();
        var store = await dbMain.Stores.SingleOrDefaultAsync(a => a.Id == ghostMember.StoreId);

        return View(new GhostOrderViewModel
        {
            StoreId = store.Id,
            StoreName = store.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, _uriResolver, store.GetStoreBlob()),
            BTCPayServerUrl = Request.GetAbsoluteRoot(),
            InvoiceId = invoiceId
        });
    }


    [HttpGet("btcpay-ghost.js")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> GetBtcPayJavascript(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var userStore = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);
        if (userStore == null || !userStore.CredentialsPopulated())
        {
            return BadRequest("Invalid BTCPay store specified");
        }
        var jsFile = await helper.GetCustomJavascript(userStore.StoreId, Request.GetAbsoluteRoot());
        if (!jsFile.succeeded)
        {
            return BadRequest(jsFile.response);
        }
        return Content(jsFile.response, "text/javascript");
    }


    private async Task<InvoiceEntity> CreateInvoiceAsync(Data.StoreData store, Tier tier, GhostMember member)
    {

        var shopifySearchTerm = $"{GHOST_MEMBER_ID_PREFIX}{member.Id}";
        var matchedExistingInvoices = await _invoiceRepository.GetInvoices(new InvoiceQuery()
        {
            TextSearch = shopifySearchTerm,
            StoreId = new[] { store.Id }
        });

        matchedExistingInvoices = matchedExistingInvoices.Where(entity =>
                entity.GetInternalTags(shopifySearchTerm).Any(s => s == member.Id.ToString())).ToArray();

        var firstInvoiceSettled =
            matchedExistingInvoices.LastOrDefault(entity =>
                new[] { "settled", "processing", "confirmed", "paid", "complete" }
                    .Contains(
                        entity.GetInvoiceState().Status.ToString().ToLower()));

        if (firstInvoiceSettled != null)
            return firstInvoiceSettled;

        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
            new CreateInvoiceRequest()
            {
                Amount = member.Frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price,
                Currency = tier.currency,
                Metadata = new JObject
                {
                    ["MemberId"] = member.Id
                },
                AdditionalSearchTerms = new[]
                {
                        member.MemberId.ToString(CultureInfo.InvariantCulture),
                        shopifySearchTerm
                }
            }, store,
            Request.GetAbsoluteRoot(), new List<string>() { shopifySearchTerm });

        return invoice;
    }
}

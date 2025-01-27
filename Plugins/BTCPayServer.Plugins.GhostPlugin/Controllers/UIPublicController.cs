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
using Newtonsoft.Json;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using BTCPayServer.Models;
using BTCPayServer.Services;
using BTCPayServer.Plugins.GhostPlugin.Data;
using System.Collections.Generic;
using BTCPayServer.Plugins.GhostPlugin.Data.Enums;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Controllers;
using BTCPayServer.Abstractions.Extensions;
using Newtonsoft.Json.Linq;
using System.Globalization;
using BTCPayServer.Client.Models;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[AllowAnonymous]
[Route("~/plugins/{storeId}/ghost/public/")]
public class UIPublicController : Controller
{
    private readonly UriResolver _uriResolver;
    private readonly StoreRepository _storeRepo;
    private readonly IHttpClientFactory _clientFactory;
    private readonly UIInvoiceController _invoiceController;
    private readonly BTCPayNetworkProvider _networkProvider;
    private readonly GhostDbContextFactory _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    public UIPublicController
        (UriResolver uriResolver,
        StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        BTCPayNetworkProvider networkProvider,
        UIInvoiceController invoiceController,
        GhostDbContextFactory dbContextFactory,
        UserManager<ApplicationUser> userManager)
    {
        _storeRepo = storeRepo;
        _uriResolver = uriResolver;
        _userManager = userManager;
        _clientFactory = clientFactory;
        _networkProvider = networkProvider;
        _dbContextFactory = dbContextFactory;
        _invoiceController = invoiceController;
    }

    private const string GHOST_MEMBER_ID_PREFIX = "ghost_member-";


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

        Console.WriteLine(JsonConvert.SerializeObject(vm, Formatting.Indented));
        var storeData = await _storeRepo.FindStore(storeId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        Tier tier = ghostTiers.FirstOrDefault(c => c.id == vm.TierId);
        if (tier == null)
            return NotFound();

        var response = await apiClient.CreateGhostMember(new CreateGhostMemberRequest { members = new List<Member>
            {
                new Member
                {
                    email = vm.Email,
                    name = vm.Name,
                    tiers = new List<MemberTier>
                    {
                        new MemberTier
                        {
                            id = vm.TierId,
                            expiry_at = vm.TierSubscriptionFrequency == TierSubscriptionFrequency.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1)
                        }
                    }
                }
            }
        });
        GhostMember entity = new GhostMember
        {
            CreatedAt = DateTime.UtcNow,
            MemberId = response.members[0].id,
            MemberUuid = response.members[0].uuid,
            Name = vm.Name,
            Email = vm.Email,
            SubscriptionId = response.members[0].subscriptions.First().id,
            TierId = vm.TierId,
            UnsubscribeUrl = response.members[0].unsubscribe_url,
            StoreId = storeId
        };
        Console.WriteLine(JsonConvert.SerializeObject(entity));
        ctx.Add(entity);
        await ctx.SaveChangesAsync();
        return RedirectToAction(nameof(CreateMember), new {  storeId });
    }


    private async Task CreateInvoiceAsync(Data.StoreData store, Tier tier, GhostMember member, TierSubscriptionFrequency frequency)
    {

        var shopifySearchTerm = $"{GHOST_MEMBER_ID_PREFIX}{member.MemberId}";
        var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest()
                {
                    Amount = frequency == TierSubscriptionFrequency.Monthly ? tier.monthly_price : tier.yearly_price,
                    Currency = tier.currency,
                    Metadata = new JObject
                    {
                        ["MemberId"] = member.MemberId,
                        ["MemberUuid"] = member.MemberUuid,
                        ["SubscriptionId"] = member.SubscriptionId
                    },
                    AdditionalSearchTerms = new[]
                    {
                            member.MemberId.ToString(CultureInfo.InvariantCulture),
                            member.MemberUuid.ToString(CultureInfo.InvariantCulture),
                            member.SubscriptionId.ToString(CultureInfo.InvariantCulture),
                            shopifySearchTerm
                    }
                }, store,
                Request.GetAbsoluteRoot(), new List<string>() { shopifySearchTerm });
        /*return Ok(new
        {
            invoiceId = invoice.Id,
            status = invoice.Status.ToString().ToLowerInvariant(),
            externalPaymentLink = Url.Action("InitiatePayment", "UIPublic", new { invoiceId = invoice.Id, shopName, orderId = shopifySearchTerm }, Request.Scheme)
        });*/
    }
}

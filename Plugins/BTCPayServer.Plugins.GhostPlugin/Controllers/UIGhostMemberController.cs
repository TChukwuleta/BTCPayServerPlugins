using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using BTCPayServer.Client;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Services.Stores;
using StoreData = BTCPayServer.Data.StoreData;
using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using System;
using Newtonsoft.Json;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;

namespace BTCPayServer.Plugins.ShopifyPlugin;


[Route("~/plugins/{storeId}/ghost/members/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class UIGhostMemberController : Controller
{
    private readonly StoreRepository _storeRepo;
    private readonly IHttpClientFactory _clientFactory;
    private readonly GhostDbContextFactory _dbContextFactory;
    public UIGhostMemberController
        (StoreRepository storeRepo,
        IHttpClientFactory clientFactory,
        GhostDbContextFactory dbContextFactory)
    {
        _storeRepo = storeRepo;
        _clientFactory = clientFactory;
        _dbContextFactory = dbContextFactory;
    }
    public StoreData CurrentStore => HttpContext.GetStoreData();

    [HttpGet("list")]
    public async Task<IActionResult> List(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var ghostSetting = ctx.GhostSettings.AsNoTracking().FirstOrDefault(c => c.StoreId == storeId);

        var storeData = await _storeRepo.FindStore(storeId);
        var apiClient = new GhostAdminApiClient(_clientFactory, ghostSetting.CreateGhsotApiCredentials());
        var ghostTiers = await apiClient.RetrieveGhostTiers();
        if (ghostTiers == null || !ghostTiers.Any())
        {
            Console.WriteLine($"Ghost tiers: {JsonConvert.SerializeObject(ghostTiers)}");
            TempData.SetStatusMessageModel(new StatusMessageModel()
            {
                Severity = StatusMessageModel.StatusSeverity.Error,
                Message = $"Cannot retrieve tier list from Ghost. Please try again later"
            });
            return RedirectToAction(nameof(Index), "UIGhost", new { storeId });
        }
        var ghostMembers = ctx.GhostMembers.AsNoTracking().Where(c => c.StoreId == storeId && !string.IsNullOrEmpty(c.MemberId)).ToList();
        var ghostTransactions = ctx.GhostTransactions.AsNoTracking().Where(t => t.StoreId == storeId && t.TransactionStatus == TransactionStatus.Success).ToList();
        var ghostMemberListViewModels = ghostMembers
            .Select(member =>
            {
                var tier = ghostTiers.FirstOrDefault(t => t.id == member.TierId);
                var transactions = ghostTransactions.Where(t => t.MemberId == member.Id).OrderByDescending(t => t.SubscriptionEndDate).ToList();
                var mostRecentTransaction = transactions.FirstOrDefault();
                return new GhostMemberListViewModel
                {
                    Id = member.Id,
                    MemberId = member.MemberId,
                    Name = member.Name,
                    Email = member.Email,
                    TierId = member.TierId,
                    StoreId = storeId,
                    Frequency = member.Frequency,
                    CreatedDate = member.CreatedAt,
                    PeriodEndDate = (DateTimeOffset)(mostRecentTransaction?.SubscriptionEndDate),
                    TierName = tier?.name ?? "",
                    Subscriptions = transactions.Select(t => new GhostTransactionViewModel
                    {
                        StoreId = storeId,
                        InvoiceId = t.InvoiceId, 
                        InvoiceStatus = t.InvoiceStatus,
                        Amount = t.Amount,
                        Currency = tier?.currency,
                        MemberId = member.MemberId,
                        PeriodStartDate = t.SubscriptionStartDate.Value,
                        PeriodEndDate = t.SubscriptionEndDate.Value
                    }).ToList()
                };
            }).ToList();
        return View(new GhostMembersViewModel { Members = ghostMemberListViewModels, StoreId = storeId });
    }

}

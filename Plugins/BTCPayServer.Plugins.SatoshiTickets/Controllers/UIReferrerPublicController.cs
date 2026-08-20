using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Helper;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.SatoshiTickets;

[AllowAnonymous]
[Route("~/plugins/{storeId}/satoshi-tickets/referral-portal/")]
public class UIReferrerPublicController(StoreRepository storeRepo, UriResolver uriResolver, SimpleTicketSalesDbContextFactory dbContextFactory) : Controller
{
    private const string ReferrerAuthSessionKey = "SatoshiTickets_Referrer_Auth_Id";

    [HttpGet("login")]
    public async Task<IActionResult> Login(string storeId)
    {
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();

        return View(new ReferrerLoginViewModel
        {
            StoreId = storeId,
            StoreName = storeData.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, storeData.GetStoreBlob())
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(string storeId, ReferrerLoginViewModel model)
    {
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();
    
        model.StoreId = storeId;
        model.StoreName = storeData.StoreName;
        model.StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, storeData.GetStoreBlob());
    
        await using var ctx = dbContextFactory.CreateContext();
        var normalizedEmail = model.Email?.Trim().ToLowerInvariant();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.StoreId == storeId && r.Email == normalizedEmail);
        if (referrer == null || referrer.State != ReferrerState.Active || !CheckInTokenHelper.VerifyPin(model.Password, referrer.PasswordHash))
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }
        HttpContext.Session.SetString(ReferrerAuthSessionKey, referrer.Id);
        return RedirectToAction(nameof(Dashboard), new { storeId });
    }

    [HttpGet("logout")]
    public IActionResult Logout(string storeId)
    {
        HttpContext.Session.Remove(ReferrerAuthSessionKey);
        return RedirectToAction(nameof(Login), new { storeId });
    }

    [HttpGet("accept-invitation/{token}")]
    public async Task<IActionResult> AcceptInvitation(string storeId, string token)
    {
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();
    
        await using var ctx = dbContextFactory.CreateContext();
        var invitation = FindValidInvitation(ctx, storeId, token);
        var vm = new AcceptReferrerInvitationViewModel
        {
            StoreId = storeId,
            StoreName = storeData.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, storeData.GetStoreBlob()),
            Token = token
        };
        if (invitation == null)
        {
            vm.ErrorMessage = "This invitation link is invalid or has expired. Ask the organizer to resend it.";
            return View(vm);
        }
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == invitation.ReferrerId && r.StoreId == storeId);
        vm.ReferrerName = referrer?.Name;
        vm.Email = referrer?.Email;
        return View(vm);
    }

    [HttpPost("accept-invitation/{token}")]
    public async Task<IActionResult> AcceptInvitation(string storeId, string token, AcceptReferrerInvitationViewModel model)
    {
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();
    
        model.StoreId = storeId;
        model.StoreName = storeData.StoreName;
        model.StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, storeData.GetStoreBlob());
        model.Token = token;
        await using var ctx = dbContextFactory.CreateContext();
        var invitation = FindValidInvitation(ctx, storeId, token);
        if (invitation == null)
        {
            model.ErrorMessage = "This invitation link is invalid or has expired. Ask the organizer to resend it.";
            return View(model);
        }
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == invitation.ReferrerId && r.StoreId == storeId);
        if (referrer == null)
        {
            model.ErrorMessage = "This referrer account no longer exists.";
            return View(model);
        }
        model.ReferrerName = referrer.Name;
        model.Email = referrer.Email;
        if (referrer.State != ReferrerState.Pending)
        {
            model.ErrorMessage = "This account has already been activated. Use the login page instead.";
            return View(model);
        }
        if (!ModelState.IsValid)
            return View(model);
    
        referrer.PasswordHash = CheckInTokenHelper.HashPin(model.NewPassword);
        referrer.State = ReferrerState.Active;
        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        HttpContext.Session.SetString(ReferrerAuthSessionKey, referrer.Id);
        return RedirectToAction(nameof(Dashboard), new { storeId });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(string storeId)
    {
        var storeData = await storeRepo.FindStore(storeId);
        if (storeData == null) return NotFound();
    
        await using var ctx = dbContextFactory.CreateContext();
        var referrer = GetAuthenticatedReferrer(ctx, storeId);
        if (referrer == null)
            return RedirectToAction(nameof(Login), new { storeId });

        var credits = ctx.ReferralCredits.Where(c => c.ReferrerId == referrer.Id && c.StoreId == storeId).OrderByDescending(c => c.CreatedAt).ToList();
        var eventIds = credits.Select(c => c.EventId).Where(id => id != null).Distinct().ToList();
        var eventTitles = ctx.Events.Where(e => eventIds.Contains(e.Id)).ToDictionary(e => e.Id, e => e.Title);
        var vm = new ReferrerDashboardViewModel
        {
            StoreId = storeId,
            StoreName = storeData.StoreName,
            StoreBranding = await StoreBrandingViewModel.CreateAsync(Request, uriResolver, storeData.GetStoreBlob()),
            ReferrerName = referrer.Name,
            AvailableBalances = credits.Where(c => c.Status == ReferralCreditStatus.Confirmed)
                .GroupBy(c => c.Currency)
                .Select(g => new ReferrerBalanceViewModel { Currency = g.Key, Amount = g.Sum(c => c.Amount) })
                .OrderBy(b => b.Currency).ToList(),
            RecentCredits = credits.Take(15).Select(c => new ReferralCreditActivityViewModel
            {
                EventTitle = c.EventId != null ? eventTitles.GetValueOrDefault(c.EventId) : null,
                Amount = c.Amount,
                Currency = c.Currency,
                Status = c.Status,
                CreatedAt = c.CreatedAt
            }).ToList()
        };
        return View(vm);
    }

    private ReferrerInvitation FindValidInvitation(SimpleTicketSalesDbContext ctx, string storeId, string token)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-UIReferrerController.InvitationValidDays);
        return ctx.ReferrerInvitations.FirstOrDefault(i => i.StoreId == storeId && i.Token == token && i.AcceptedAt == null && i.CreatedAt >= cutoff);
    }

    private Referrer GetAuthenticatedReferrer(SimpleTicketSalesDbContext ctx, string storeId)
    {
        var referrerId = HttpContext.Session.GetString(ReferrerAuthSessionKey);
        if (string.IsNullOrEmpty(referrerId))
            return null;
    
        return ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == storeId && r.State == ReferrerState.Active);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using BTCPayServer.Plugins.SatoshiTickets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.SatoshiTickets;

[Route("~/plugins/{storeId}/satoshi-tickets/referrers/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[AutoValidateAntiforgeryToken]
public class UIReferrerController(SimpleTicketSalesDbContextFactory dbContextFactory, EmailService emailService) : Controller
{
    public const int InvitationValidDays = 7;
    private StoreData CurrentStore => HttpContext.GetStoreData();
    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private async Task<bool> SendInvitation(SimpleTicketSalesDbContext ctx, Referrer referrer, string storeId)
    {
        var invitation = new ReferrerInvitation
        {
            ReferrerId = referrer.Id,
            StoreId = storeId,
            Email = referrer.Email,
            Token = GenerateToken(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        ctx.ReferrerInvitations.Add(invitation);
        await ctx.SaveChangesAsync();
        var acceptUrl = Url.Action(nameof(UIReferrerPublicController.AcceptInvitation), "UIReferrerPublic", new { storeId, token = invitation.Token }, Request.Scheme);
        var result = await emailService.SendReferrerInvitationEmail(storeId, referrer.Email, referrer.Name, CurrentStore.StoreName, acceptUrl);
        return result.IsSuccessful;
    }

    [HttpGet("")]
    public async Task<IActionResult> Referrers(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrers = ctx.Referrers.Where(r => r.StoreId == CurrentStore.Id).OrderByDescending(r => r.CreatedAt).ToList();

        var referrerIds = referrers.Select(r => r.Id).ToList();
        var balancesByReferrer = ctx.ReferralCredits
                    .Where(c => c.StoreId == CurrentStore.Id && referrerIds.Contains(c.ReferrerId) && c.Status == ReferralCreditStatus.Confirmed)
                    .GroupBy(c => new { c.ReferrerId, c.Currency })
                    .Select(g => new { g.Key.ReferrerId, g.Key.Currency, Amount = g.Sum(c => c.Amount) })
                    .ToList()
                    .GroupBy(x => x.ReferrerId)
                    .ToDictionary(g => g.Key, g => g.Select(x => new ReferrerBalanceViewModel { Currency = x.Currency, Amount = x.Amount })
                        .OrderBy(b => b.Currency).ToList());

        var vm = new ReferrerListViewModel
        {
            StoreId = CurrentStore.Id,
            Referrers = referrers.Select(r => new ReferrerListItemViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Email = r.Email,
                State = r.State,
                CreatedAt = r.CreatedAt,
                AvailableBalances = balancesByReferrer.GetValueOrDefault(r.Id) ?? new List<ReferrerBalanceViewModel>()
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet("create")]
    public IActionResult CreateReferrer(string storeId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        return View(new CreateReferrerViewModel { StoreId = CurrentStore.Id });
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateReferrer(string storeId, CreateReferrerViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        vm.StoreId = CurrentStore.Id;
        if (string.IsNullOrWhiteSpace(vm.Name))
            ModelState.AddModelError(nameof(vm.Name), "A name is required.");

        var normalizedEmail = vm.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            ModelState.AddModelError(nameof(vm.Email), "An email is required to send the invitation.");

        await using var ctx = dbContextFactory.CreateContext();
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && ctx.Referrers.Any(r => r.StoreId == CurrentStore.Id && r.Email == normalizedEmail))
        {
            ModelState.AddModelError(nameof(vm.Email), "A referrer with this email already exists for this store.");
        }

        if (!ModelState.IsValid)
            return View(vm);

        var referrer = new Referrer
        {
            StoreId = CurrentStore.Id,
            Name = vm.Name.Trim(),
            Email = normalizedEmail,
            State = ReferrerState.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        ctx.Referrers.Add(referrer);
        await ctx.SaveChangesAsync();
        var emailSent = await SendInvitation(ctx, referrer, storeId);
        TempData[WellKnownTempData.SuccessMessage] = emailSent
                   ? $"Referrer created and an invitation was emailed to {referrer.Email}."
                   : $"Referrer created, but the invitation email could not be sent (check the store's email settings). Use \"Resend Invitation\" once that's fixed.";
        return RedirectToAction(nameof(Referrers), new { storeId });
    }

    [HttpGet("{referrerId}/edit")]
    public async Task<IActionResult> EditReferrer(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        var vm = new EditReferrerViewModel
        {
            Id = referrer.Id,
            StoreId = CurrentStore.Id,
            Name = referrer.Name,
            Email = referrer.Email,
            State = referrer.State
        };
        LoadReferrerCreditData(ctx, vm);
        return View(vm);
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveReferrer(string storeId, EditReferrerViewModel vm)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        if (string.IsNullOrWhiteSpace(vm.Name))
            ModelState.AddModelError(nameof(vm.Name), "A name is required.");

        await using var ctx = dbContextFactory.CreateContext();
        if (!ModelState.IsValid)
        {
            vm.StoreId = CurrentStore.Id;
            LoadReferrerCreditData(ctx, vm);
            return View(nameof(EditReferrer), vm);
        }

        var existing = ctx.Referrers.FirstOrDefault(r => r.Id == vm.Id && r.StoreId == CurrentStore.Id);
        if (existing == null) return NotFound();
        existing.Name = vm.Name.Trim();
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Referrer saved successfully";
        return RedirectToAction(nameof(Referrers), new { storeId });
    }

    [HttpGet("{referrerId}/record-payout")]
    public async Task<IActionResult> RecordPayout(string storeId, string referrerId, string currency)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        var totalAmount = ctx.ReferralCredits.Where(c => c.ReferrerId == referrerId && c.StoreId == CurrentStore.Id
                     && c.Status == ReferralCreditStatus.Confirmed && c.Currency == currency).Sum(c => (decimal?)c.Amount) ?? 0;
        if (totalAmount <= 0)
        {
            TempData[WellKnownTempData.ErrorMessage] = "There's no confirmed balance in that currency to pay out.";
            return RedirectToAction(nameof(EditReferrer), new { storeId, referrerId });
        }
        return View(new RecordPayoutConfirmViewModel
        {
            StoreId = storeId,
            ReferrerId = referrerId,
            ReferrerName = referrer.Name,
            Currency = currency,
            Amount = totalAmount
        });
    }

    [HttpPost("{referrerId}/record-payout")]
    public async Task<IActionResult> RecordPayoutPost(string storeId, string referrerId, string currency, string note)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        await using var transaction = await ctx.Database.BeginTransactionAsync();
        var payout = new ReferralPayout
        {
            StoreId = CurrentStore.Id,
            ReferrerId = referrerId,
            Amount = 0,
            Currency = currency,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        ctx.ReferralPayouts.Add(payout);
        await ctx.SaveChangesAsync();

        var claimed = await ctx.ReferralCredits.Where(c => c.ReferrerId == referrerId && c.StoreId == CurrentStore.Id
                     && c.Status == ReferralCreditStatus.Confirmed && c.Currency == currency)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ReferralCreditStatus.PaidOut).SetProperty(c => c.PayoutId, payout.Id));

        if (claimed == 0)
        {
            await transaction.RollbackAsync();
            TempData[WellKnownTempData.ErrorMessage] = "There's no confirmed balance in that currency to pay out.";
            return RedirectToAction(nameof(EditReferrer), new { storeId, referrerId });
        }
        var totalAmount = await ctx.ReferralCredits.Where(c => c.PayoutId == payout.Id).SumAsync(c => c.Amount);
        payout.Amount = totalAmount;
        await ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Recorded a payout of {totalAmount:N2} {currency} to {referrer.Name}.";
        return RedirectToAction(nameof(EditReferrer), new { storeId, referrerId });
    }

    [HttpGet("{referrerId}/toggle")]
    public async Task<IActionResult> ToggleReferrer(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        if (referrer.State == ReferrerState.Pending)
        {
            TempData[WellKnownTempData.ErrorMessage] = "This referrer hasn't accepted their invitation yet.";
            return RedirectToAction(nameof(Referrers), new { storeId });
        }

        var activating = referrer.State != ReferrerState.Active;
        var action = activating ? "Activate" : "Disable";
        return View("Confirm", new ConfirmModel($"{action} referrer",
            $"<strong>{referrer.Name}</strong> will be {(activating ? "activated" : "disabled")}. Are you sure?", action)
        {
            ActionName = nameof(ToggleReferrerPost),
            ActionValues = new { storeId, referrerId },
            Antiforgery = true
        });
    }

    [HttpPost("{referrerId}/toggle")]
    public async Task<IActionResult> ToggleReferrerPost(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        if (referrer.State == ReferrerState.Pending)
        {
            TempData[WellKnownTempData.ErrorMessage] = "This referrer hasn't accepted their invitation yet.";
            return RedirectToAction(nameof(Referrers), new { storeId });
        }
        referrer.State = referrer.State == ReferrerState.Active ? ReferrerState.Disabled : ReferrerState.Active;
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = $"Referrer {(referrer.State == ReferrerState.Active ? "activated" : "disabled")} successfully";
        return RedirectToAction(nameof(Referrers), new { storeId });
    }

    [HttpGet("{referrerId}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        if (referrer.State != ReferrerState.Pending)
        {
            TempData[WellKnownTempData.ErrorMessage] = "This referrer has already activated their account.";
            return RedirectToAction(nameof(Referrers), new { storeId });
        }

        return View("Confirm", new ConfirmModel("Resend invitation",
            $"A fresh invitation link will be emailed to <strong>{referrer.Email}</strong>, replacing the current 7-day window.", "Resend invitation")
        {
            ActionName = nameof(ResendInvitationPost),
            ActionValues = new { storeId, referrerId }
        });
    }

    [HttpPost("{referrerId}/resend-invitation")]
    public async Task<IActionResult> ResendInvitationPost(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        if (referrer.State != ReferrerState.Pending)
        {
            TempData[WellKnownTempData.ErrorMessage] = "This referrer has already activated their account.";
            return RedirectToAction(nameof(Referrers), new { storeId });
        }
        var emailSent = await SendInvitation(ctx, referrer, storeId);
        TempData[emailSent ? WellKnownTempData.SuccessMessage : WellKnownTempData.ErrorMessage] = emailSent
                   ? $"Invitation resent to {referrer.Email}."
                   : "Could not send the invitation email - check the store's email settings.";
        return RedirectToAction(nameof(Referrers), new { storeId });
    }

    [HttpGet("{referrerId}/delete")]
    public async Task<IActionResult> DeleteReferrer(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        return View("Confirm", new ConfirmModel("Delete referrer",
            $"The referrer <strong>{referrer.Name}</strong> will be permanently deleted. This cannot be undone.", "Delete")
        {
            ActionName = nameof(DeleteReferrerPost),
            ActionValues = new { storeId, referrerId }
        });
    }

    [HttpPost("{referrerId}/delete")]
    public async Task<IActionResult> DeleteReferrerPost(string storeId, string referrerId)
    {
        if (string.IsNullOrEmpty(CurrentStore.Id))
            return NotFound();

        await using var ctx = dbContextFactory.CreateContext();
        var referrer = ctx.Referrers.FirstOrDefault(r => r.Id == referrerId && r.StoreId == CurrentStore.Id);
        if (referrer == null) return NotFound();

        var invitations = ctx.ReferrerInvitations.Where(i => i.ReferrerId == referrer.Id).ToList();
        ctx.ReferrerInvitations.RemoveRange(invitations);
        ctx.Referrers.Remove(referrer);
        await ctx.SaveChangesAsync();
        TempData[WellKnownTempData.SuccessMessage] = "Referrer deleted";
        return RedirectToAction(nameof(Referrers), new { storeId });
    }

    private static void LoadReferrerCreditData(SimpleTicketSalesDbContext ctx, EditReferrerViewModel vm)
    {
        var credits = ctx.ReferralCredits.Where(c => c.ReferrerId == vm.Id && c.StoreId == vm.StoreId).OrderByDescending(c => c.CreatedAt).ToList();
        var eventIds = credits.Select(c => c.EventId).Where(id => id != null).Distinct().ToList();
        var eventTitles = ctx.Events.Where(e => eventIds.Contains(e.Id)).ToDictionary(e => e.Id, e => e.Title);
        var payouts = ctx.ReferralPayouts.Where(p => p.ReferrerId == vm.Id && p.StoreId == vm.StoreId).OrderByDescending(p => p.CreatedAt).ToList();

        vm.AvailableBalances = credits.Where(c => c.Status == ReferralCreditStatus.Confirmed)
            .GroupBy(c => c.Currency)
            .Select(g => new ReferrerBalanceViewModel { Currency = g.Key, Amount = g.Sum(c => c.Amount) })
            .OrderBy(b => b.Currency).ToList();

        vm.RecentCredits = credits.Take(25).Select(c => new ReferralCreditActivityViewModel
        {
            EventTitle = c.EventId != null ? eventTitles.GetValueOrDefault(c.EventId) : null,
            Amount = c.Amount,
            Currency = c.Currency,
            Status = c.Status,
            CreatedAt = c.CreatedAt
        }).ToList();

        vm.PayoutHistory = payouts.Select(p => new ReferralPayoutActivityViewModel
        {
            Amount = p.Amount,
            Currency = p.Currency,
            Note = p.Note,
            CreatedAt = p.CreatedAt
        }).ToList();
    }
}
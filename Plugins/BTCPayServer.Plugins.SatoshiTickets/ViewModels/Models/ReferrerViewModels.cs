using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

internal class ReferrerViewModels
{
}

public class ReferrerListViewModel
{
    public string StoreId { get; set; }
    public List<ReferrerListItemViewModel> Referrers { get; set; } = new();
}

public class ReferrerListItemViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public ReferrerState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ReferrerBalanceViewModel> AvailableBalances { get; set; } = new();
}

public class ReferrerBalanceViewModel
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}

public class ReferralCreditActivityViewModel
{
    public string EventTitle { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public ReferralCreditStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ReferralPayoutActivityViewModel
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
 }

public class CreateReferrerViewModel
{
    public string StoreId { get; set; }
    [Required]
    [Display(Name = "Name")]
    public string Name { get; set; }
    [Required]
    [Display(Name = "Email")]
    public string Email { get; set; }
}

public class EditReferrerViewModel
{
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public ReferrerState State { get; set; }
    public List<ReferrerBalanceViewModel> AvailableBalances { get; set; } = new();
    public List<ReferralCreditActivityViewModel> RecentCredits { get; set; } = new();
    public List<ReferralPayoutActivityViewModel> PayoutHistory { get; set; } = new();
}

public class ReferrerLoginViewModel : BaseSimpleTicketPublicViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public string ErrorMessage { get; set; }
}

public class AcceptReferrerInvitationViewModel : BaseSimpleTicketPublicViewModel
{
    public string Token { get; set; }
    public string ReferrerName { get; set; }
    public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; }

    public string ErrorMessage { get; set; }
}

public class ReferrerDashboardViewModel : BaseSimpleTicketPublicViewModel
{
    public string ReferrerName { get; set; }
    public List<ReferrerBalanceViewModel> AvailableBalances { get; set; } = new();
    public List<ReferralCreditActivityViewModel> RecentCredits { get; set; } = new();
}

public class RecordPayoutConfirmViewModel
{
    public string StoreId { get; set; }
    public string ReferrerId { get; set; }
    public string Currency { get; set; }
    public string ReferrerName { get; set; }
    public decimal Amount { get; set; }
}
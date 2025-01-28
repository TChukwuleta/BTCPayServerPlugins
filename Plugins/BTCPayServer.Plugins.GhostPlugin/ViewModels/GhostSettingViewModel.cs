using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class GhostSettingViewModel
{

    [Display(Name = "Ghost API URL")]
    public string ApiUrl { get; set; }

    [Display(Name = "Admin API Key")]
    public string AdminApiKey { get; set; }

    [Display(Name = "Content API Key")]
    public string ContentApiKey { get; set; }

    [Display(Name = "Ghost Username/Email")]
    public string Username { get; set; }
    public string Password { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public bool HasWallet { get; set; } = true;
    public string CryptoCode { get; set; }

    [Display(Name = "Membership subscription Url")]
    public string MemberCreationUrl { get; set; }

    [Display(Name = "Donation Url")]
    public string DonationUrl { get; set; }

    public DateTimeOffset? IntegratedAt { get; set; }
}

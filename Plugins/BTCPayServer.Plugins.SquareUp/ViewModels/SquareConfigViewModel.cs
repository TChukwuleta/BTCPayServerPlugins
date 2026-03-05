using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.SquareUp.Data;

namespace BTCPayServer.Plugins.SquareUp.ViewModels;

public class SquareConfigViewModel
{
    public string StoreId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Access Token is required")]
    [Display(Name = "Square Access Token")]
    public string AccessToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location ID is required")]
    [Display(Name = "Square Location ID")]
    public string LocationId { get; set; } = string.Empty;

    [Display(Name = "Payout Preference")]
    public PayoutPreference PayoutPreference { get; set; } = PayoutPreference.Fiat;

    [Display(Name = "Use Sandbox (testing)")]
    public bool IsSandbox { get; set; } = false;

    [Display(Name = "Square Webhook Signature Key (optional)")]
    public string SquareWebhookSignatureKey { get; set; }
    public string ConnectionStatus { get; set; }
    public bool ConnectionSuccess { get; set; }
}

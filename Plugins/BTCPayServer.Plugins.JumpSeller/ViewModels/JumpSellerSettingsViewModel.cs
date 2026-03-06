using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.JumpSeller.ViewModels;

public class JumpSellerSettingsViewModel
{
    [Required]
    [Display(Name = "Payment Method Key")]
    public string EpgAccountId { get; set; }

    [Required]
    [Display(Name = "Payment Method Secret")]
    public string EpgSecret { get; set; }
    public string StoreId { get; set; }
    public string PaymentUrl { get; set; }
}

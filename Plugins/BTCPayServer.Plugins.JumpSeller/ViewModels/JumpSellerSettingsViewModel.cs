using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.JumpSeller.ViewModels;

public class JumpSellerSettingsViewModel
{
    [Required]
    [Display(Name = "EPG Account ID")]
    public string EpgAccountId { get; set; }

    [Required]
    [Display(Name = "EPG Secret")]
    public string EpgSecret { get; set; }
    public string StoreId { get; set; }
    public string PaymentUrl { get; set; }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class MavapaySettingViewModel
{

    [Display(Name = "Enable Split Payment")]
    public bool EnableSplitPayment { get; set; }
    public SplitPaymentSettingsViewModel SplitPayment { get; set; }
}


public class SplitPaymentSettingsViewModel
{
    public enum MavapayCurrency
    {
        NGN,
        KES,
        ZAR
    }

    public int SplitPercentage { get; set; }
    public MavapayCurrency Currency { get; set; }
    public string NGNBankCode { get; set; }
    public string NGNAccountNumber { get; set; }
    public string NGNAccountName { get; set; }
    public string NGNBankName { get; set; }
    public string KESMethod { get; set; }  // TillNumber / PhoneNumber / BillNumber
    public string KESIdentifier { get; set; }
    public string KESAccountName { get; set; }
    public string KESAccountNumber { get; set; }
    public string ZARBank { get; set; }
    public string ZARAccountNumber { get; set; }
    public string ZARAccountName { get; set; } 
    public IEnumerable<SelectListItem> NGNBanks { get; set; }
    public IEnumerable<SelectListItem> ZARBanks { get; set; }
    public IEnumerable<SelectListItem> KESPaymentMethod { get; set; }
}

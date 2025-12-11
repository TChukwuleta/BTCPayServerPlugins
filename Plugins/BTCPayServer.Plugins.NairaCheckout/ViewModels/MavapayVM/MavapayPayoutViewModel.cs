using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class MavapayPayoutViewModel
{
    public PayoutZARViewModel ZAR { get; set; } = new();
    public PayoutNGNViewModel NGN { get; set; } = new();
    public PayoutKESViewModel KES { get; set; } = new();
    public List<SelectListItem> NGNBanks { get; set; }
    public List<SelectListItem> ZARBanks { get; set; }
    public List<SelectListItem> KESPaymentMethod { get; set; }
}

public class PayoutZARViewModel
{
    public string Bank { get; set; }
    public string AccountNumber { get; set; }
    public string AccountName { get; set; }
    public decimal Amount { get; set; }
}

public class PayoutNGNViewModel
{
    [Required]
    public string BankCode { get; set; }
    [Required]
    public string BankName { get; set; }
    public string AccountNumber { get; set; }

    [Range(4000, double.MaxValue, ErrorMessage = "Amount must be greater than 4000")]
    public decimal Amount { get; set; }
    public string AccountName { get; set; }
}

public class PayoutKESViewModel
{
    public string Method { get; set; }
    public string AccountNumber { get; set; }
    public string AccountName { get; set; }
    public string Identifier { get; set; }
    public decimal Amount { get; set; }
}

public class BankViewModel
{
    public string Code { get; set; }
    public string Name { get; set; }
}


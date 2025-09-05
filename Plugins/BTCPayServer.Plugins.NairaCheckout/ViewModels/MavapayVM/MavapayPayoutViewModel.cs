using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class MavapayPayoutViewModel
{
    public PayoutZARViewModel ZAR { get; set; } = new();
    public PayoutNGNViewModel NGN { get; set; } = new();
    public PayoutKESViewModel KES { get; set; } = new();
    public List<SelectListItem> NGNBanks { get; set; }
}

public class PayoutZARViewModel
{
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
}

public class PayoutNGNViewModel
{
    public string BankCode { get; set; }
    public string BankName { get; set; }
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string AccountName { get; set; }
}

public class PayoutKESViewModel
{
    public string Method { get; set; } // BillNumber, TillNumber, PhoneNumber
    public string AccountNumber { get; set; }
    public string Identifier { get; set; } // till/phone/bill
    public decimal Amount { get; set; }
}

public class BankViewModel
{
    public string Code { get; set; }
    public string Name { get; set; }
}


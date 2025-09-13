using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.NairaCheckout.Data;

public enum Wallet
{
    Mavapay
}


public enum SupportedCurrency
{
    NGN = 1,
    ZAR, 
    KES
}

public enum MpesaPaymentMethod
{
    [Display(Name = "Phone Number")]
    PhoneNumber,
    [Display(Name = "Till Number")]
    TillNumber,
    [Display(Name = "Bill Number")]
    BillNumber
}
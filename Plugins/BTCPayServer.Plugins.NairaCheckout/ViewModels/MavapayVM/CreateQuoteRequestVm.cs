namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class CreateQuoteRequestVm
{
    public decimal amount { get; set; }
    public int customerInternalFee { get; set; }
    public string sourceCurrency { get; set; }
    public string targetCurrency { get; set; }
    public string paymentMethod { get; set; }
    public MavapayBeneficiaryVm beneficiary { get; set; }
    public bool autopayout { get; set; }
}

public class MavapayBeneficiaryVm
{
    public string lnInvoice { get; set; }
}


public class CreatePayoutRequestVm
{
    public decimal amount { get; set; }
    public int customerInternalFee { get; set; }
    public string sourceCurrency { get; set; }
    public string targetCurrency { get; set; }
    public string paymentMethod { get; set; }
    public PayoutBeneficiary beneficiary { get; set; }
    public bool autopayout { get; set; }
    public string paymentCurrency { get; set; }
}

public class PayoutBeneficiary
{
    public string bankAccountNumber { get; set; }
    public string bankAccountName { get; set; }
    public string bankCode { get; set; }
    public string bankName { get; set; }
}
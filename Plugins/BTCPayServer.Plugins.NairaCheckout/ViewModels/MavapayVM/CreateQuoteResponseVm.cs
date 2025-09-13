using System;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class CreateQuoteResponseVm
{
    public string id { get; set; }
    public int exchangeRate { get; set; }
    public double usdToTargetCurrencyRate { get; set; }
    public string sourceCurrency { get; set; }
    public string targetCurrency { get; set; }
    public int transactionFeesInSourceCurrency { get; set; }
    public int transactionFeesInTargetCurrency { get; set; }
    public int amountInSourceCurrency { get; set; }
    public int amountInTargetCurrency { get; set; }
    public string paymentMethod { get; set; }
    public DateTime expiry { get; set; }
    public bool isValid { get; set; }
    public string invoice { get; set; }
    public string hash { get; set; }
    public int totalAmountInSourceCurrency { get; set; }
    public int customerInternalFee { get; set; }
    public string bankName { get; set; }
    public string ngnBankAccountNumber { get; set; }
    public string ngnAccountName { get; set; }
    public int estimatedRoutingFee { get; set; }
    public string orderId { get; set; }
}

public class NairaCheckoutResponseViewModel
{
    public string BankName { get; set; }
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string LightningInvoice { get; set; }
    public string AccountName { get; set; }
    public string ErrorMessage { get; set; }
    public DateTimeOffset? AccountNumberExpiration { get; set; }
}

public class CreatePayoutResponseModel
{
    public string id { get; set; }
    public double exchangeRate { get; set; }
    public double usdToTargetCurrencyRate { get; set; }
    public string sourceCurrency { get; set; }
    public string targetCurrency { get; set; }
    public int transactionFeesInSourceCurrency { get; set; }
    public int transactionFeesInTargetCurrency { get; set; }
    public int amountInSourceCurrency { get; set; }
    public int amountInTargetCurrency { get; set; }
    public string paymentMethod { get; set; }
    public DateTime expiry { get; set; }
    public bool isValid { get; set; }
    public string invoice { get; set; }
    public string hash { get; set; }
    public int totalAmountInSourceCurrency { get; set; }
    public int customerInternalFee { get; set; }
    public string orderId { get; set; }
    public string ErrorMessage { get; set; }
}
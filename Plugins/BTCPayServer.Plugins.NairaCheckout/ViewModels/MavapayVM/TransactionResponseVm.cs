using System;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class TransactionResponseVm
{
    public string id { get; set; }
    public string @ref { get; set; }
    public string hash { get; set; }
    public int amount { get; set; }
    public int fees { get; set; }
    public string currency { get; set; }
    public string type { get; set; }
    public string status { get; set; }
    public bool autopayout { get; set; }
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
    public Metadata metadata { get; set; }
}
public class Metadata
{
    public string id { get; set; }
    public string merchantId { get; set; }
    public string merchantName { get; set; }
    public string bankName { get; set; }
    public string bankAccountNumber { get; set; }
    public string reference { get; set; }
    public int customerInternalFee { get; set; }
    public Order order { get; set; }
}

public class Order
{
    public string id { get; set; }
    public string settledQuoteId { get; set; }
    public int amount { get; set; }
    public string currency { get; set; }
    public string paymentMethod { get; set; }
    public string status { get; set; }
    public Quote quote { get; set; }
}

public class Quote
{
    public string id { get; set; }
    public double exchangeRate { get; set; }
    public double usdToTargetCurrencyRate { get; set; }
    public string paymentBtcDetail { get; set; }
    public string paymentMethod { get; set; }
    public int totalAmount { get; set; }
    public int equivalentAmount { get; set; }
    public DateTime expiry { get; set; }
    public string sourceCurrency { get; set; }
    public string targetCurrency { get; set; }
    public string paymentCurrency { get; set; }
    public int customerInternalFee { get; set; }
}
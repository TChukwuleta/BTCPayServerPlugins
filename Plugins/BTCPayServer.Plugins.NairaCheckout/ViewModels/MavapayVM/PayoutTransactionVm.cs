using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class PayoutTransactionVm
{
    public string Provider { get; set; }
    public string PullPaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public bool IsSuccess { get; set; }
    public string ExternalReference { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}


public class PayoutListViewModel
{
    public string SearchText { get; set; }
    public List<PayoutTransactionVm> PayoutTransactions { get; set; }
}
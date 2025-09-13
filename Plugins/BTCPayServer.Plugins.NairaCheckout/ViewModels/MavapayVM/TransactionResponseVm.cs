using System;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class TransactionResponseVm
{
    public string id { get; set; }
    public string walletId { get; set; }
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
    public object metadata { get; set; }
}
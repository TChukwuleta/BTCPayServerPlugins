using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.NairaCheckout.Data;

public class PayoutTransaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string Provider { get; set; }
    public decimal Amount { get; set; }
    public string PullPaymentId { get; set; }
    public string BaseCurrency { get; set; }
    public string Currency { get; set; }
    public string Identifier { get; set; }
    public string Data { get; set; }
    public bool IsSuccess { get; set; }
    public string ExternalReference { get; set; }
    public string ThirdPartyStatus { get; set; }
    public string StoreId { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

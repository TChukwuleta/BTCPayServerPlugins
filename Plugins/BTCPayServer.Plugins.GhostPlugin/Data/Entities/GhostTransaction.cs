using System;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostTransaction
{
    public string StoreId { get; set; }
    public string InvoiceId { get; set; }
    public string InvoiceStatus { get; set; }
    public decimal Amount { get; set; }
    public string TierId { get; set; }
    public string MemberId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
}

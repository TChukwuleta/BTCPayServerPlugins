using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class ReferralCredit
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    public string StoreId { get; set; }
    public string ReferrerId { get; set; }
    public string EventId { get; set; }
    public string OrderId { get; set; }
    public string DiscountCodeId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public ReferralCreditStatus Status { get; set; }
    public string PayoutId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
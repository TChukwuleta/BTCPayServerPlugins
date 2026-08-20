using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class ReferralPayout
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string ReferrerId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
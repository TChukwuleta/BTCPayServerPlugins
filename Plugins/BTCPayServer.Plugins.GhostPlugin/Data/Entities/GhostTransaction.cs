using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostTransaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string TxnId { get; set; }
    public string StoreId { get; set; }
    public string InvoiceId { get; set; }
    public string Currency { get; set; }
    public string InvoiceStatus { get; set; }
    public decimal Amount { get; set; }
    public string TierId { get; set; }
    public string PaymentRequestId { get; set; }
    public string MemberId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TransactionStatus TransactionStatus { get; set; }

    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTime PeriodStart { get; set; }
    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTime PeriodEnd { get; set; }

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

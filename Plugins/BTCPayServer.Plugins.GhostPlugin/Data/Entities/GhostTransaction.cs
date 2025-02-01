using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostTransaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string InvoiceId { get; set; }
    public string InvoiceStatus { get; set; }
    public decimal Amount { get; set; }
    public string TierId { get; set; }
    public string MemberId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? SubscriptionEndDate { get; set; }
    public DateTimeOffset? SubscriptionStartDate { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

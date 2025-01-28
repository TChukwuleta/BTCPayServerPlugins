using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostMember
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string MemberId { get; set; }
    public string MemberUuid { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string SubscriptionId { get; set; }
    public string TierId { get; set; }
    public string UnsubscribeUrl { get; set; }
    public string StoreId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

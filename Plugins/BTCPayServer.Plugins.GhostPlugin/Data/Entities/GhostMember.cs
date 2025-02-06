using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Converters;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostMember
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string MemberId { get; set; }
    public string MemberUuid { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string TierId { get; set; }
    public string TierName { get; set; }
    public string UnsubscribeUrl { get; set; }
    public string StoreId { get; set; }
    public TierSubscriptionFrequency Frequency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public GhostSubscriptionStatus Status { get; set; }
    public DateTimeOffset? LastReminderSent { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

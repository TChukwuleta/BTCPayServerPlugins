using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using BTCPayServer.JsonConverters;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostEvent
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    [JsonConverter(typeof(UnresolvedUriJsonConverter))]
    public UnresolvedUri EventImageUrl { get; set; }
    public string EventLink { get; set; }
    public DateTime EventDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }
    public bool HasMaximumCapacity { get; set; }
    public int? MaximumEventCapacity { get; set; }
    //public List<GhostEventTicket> Tickets { get; set; } // Different ticket types
}

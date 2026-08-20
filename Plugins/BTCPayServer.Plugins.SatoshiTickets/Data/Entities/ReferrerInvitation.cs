using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class ReferrerInvitation
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string ReferrerId { get; set; }
    public string StoreId { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
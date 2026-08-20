using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class Referrer
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public ReferrerState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
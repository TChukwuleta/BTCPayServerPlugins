using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.LightSpeed.Data;

public class LightSpeedPayment
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string InvoiceId { get; set; }
    public string StoreId { get; set; }
    public string RegisterSaleId { get; set; } // Lightspeed sale ID
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public string Status { get; set; }
}

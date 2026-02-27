using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.JumpSeller.Data;

public class JumpSellerInvoice
{
    [Key]
    public string InvoiceId { get; set; }
    public string StoreId { get; set; }
    public string OrderReference { get; set; }
    public string CallbackUrl { get; set; }
    public string CompleteUrl { get; set; }
    public string CancelUrl { get; set; }
    public string Amount { get; set; }
    public string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool CallbackSent { get; set; } = false;
    public string? LastResult { get; set; }
}

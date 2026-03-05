using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SquareUp.Data;

public class SquareUpOrderMapping
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; } 
    public string SquareOrderId { get; set; } 
    public string BTCPayInvoiceId { get; set; } 
    public long AmountInCents { get; set; } // Original fiat amount (in smallest currency unit, e.g. cents)
    public string Currency { get; set; }
    public SquareSurface SquareContactPoint { get; set; }
    public MappingStatus Status { get; set; } 
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SettledAt { get; set; }
}

public enum SquareSurface
{
    POS,  OnlineCheckout, Invoice, VirtualTerminal
}

public enum MappingStatus
{
    Pending, Settled, Expired, SyncFailed
}
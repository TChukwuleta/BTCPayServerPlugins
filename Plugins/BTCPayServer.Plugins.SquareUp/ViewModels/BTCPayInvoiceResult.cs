using System;

namespace BTCPayServer.Plugins.SquareUp.ViewModels;

public class BTCPayInvoiceResult
{
    public string InvoiceUrl { get; set; }
    public string InvoiceId { get; set; }
    public decimal BTCAmount { get; set; }
    public string? LightningInvoice { get; set; }
    public string? OnChainAddress { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SquareSpace.Data;

public class SquareSpaceOrder
{
    [Key]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string SquarespaceOrderId { get; set; }
    public string SquarespaceOrderNumber { get; set; }
    public string InvoiceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Pending";
}

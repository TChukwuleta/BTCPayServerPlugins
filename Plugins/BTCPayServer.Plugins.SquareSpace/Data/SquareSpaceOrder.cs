using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SquareSpace.Data;

public class SquareSpaceOrder
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string CartData { get; set; }
    public string CartToken { get; set; }
    public string CartId { get; set; }
    public string SquarespaceOrderId { get; set; }
    public string SquarespaceOrderNumber { get; set; }
    public string InvoiceId { get; set; }
    public string ShippingAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string CustomerEmail { get; set; }
    public string Currency { get; set; }
    public decimal Amount { get; set; }
    public string Items { get; set; }
}

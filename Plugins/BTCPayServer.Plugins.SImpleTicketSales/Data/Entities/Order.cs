using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class Order
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string EventId { get; set; }
    public string StoreId { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public decimal TotalAmount { get; set; }
    public string InvoiceId { get; set; }
    public string PaymentStatus { get; set; }
    public string InvoiceStatus { get; set; }
    public string TxnId { get; set; }
    public bool EmailSent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
}

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class Ticket
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string TicketTypeId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool EmailSent { get; set; }
    public string QRCodeData { get; set; }
    public string PaymentStatus { get; set; }
    public string AccessLink { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

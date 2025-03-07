﻿using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SimpleTicketSales.Data;

public class TicketSalesEventTicket
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string InvoiceId { get; set; }
    public string PaymentStatus { get; set; }
    public string InvoiceStatus { get; set; }
    public bool EmailSent { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

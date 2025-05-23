﻿using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class Ticket
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string TicketTypeId { get; set; }
    public string TicketTypeName { get; set; }
    public decimal Amount { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string TicketNumber { get; set; }
    public string TxnNumber { get; set; }
    public string Email { get; set; }
    public bool EmailSent { get; set; }
    public string QRCodeLink { get; set; }
    public string PaymentStatus { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

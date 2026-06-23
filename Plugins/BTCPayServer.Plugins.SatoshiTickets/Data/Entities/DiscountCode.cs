using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class DiscountCode
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    public string StoreId { get; set; }
    public string EventId { get; set; }
    public string TicketTypeId { get; set; }
    public string Code { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public int? MaxUses { get; set; }
    public int UsesCount { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public int? MinQuantity { get; set; }
    public EntityState DiscountCodeState { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

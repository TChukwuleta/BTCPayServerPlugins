using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.NairaCheckout.Data;

public class NairaCheckoutOrder
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Amount { get; set; }
    public string InvoiceId { get; set; }
    public string ExternalReference { get; set; }
    public string ExternalHash { get; set; }
    public string InvoiceStatus { get; set; }
    public bool BTCPayMarkedPaid { get; set; }
    public bool ThirdPartyMarkedPaid { get; set; }
    public string ThirdPartyStatus { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

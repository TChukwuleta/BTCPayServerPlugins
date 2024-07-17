using BTCPayServer.Services.Invoices;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.BigCommercePlugin.Data;

public class Transaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string ClientId { get; set; }
    public string StoreHash { get; set; }
    public string StoreId { get; set; }
    public string OrderId { get; set; }
    public string InvoiceId { get; set; }
    public InvoiceStatusLegacy InvoiceStatus { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
}

public enum TransactionStatus
{
    Pending = 1,
    Failed = 2,
    Success = 3
}

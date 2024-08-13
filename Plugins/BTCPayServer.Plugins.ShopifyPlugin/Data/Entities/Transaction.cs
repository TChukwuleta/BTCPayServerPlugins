using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;
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


    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}



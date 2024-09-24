using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.ShopifyPlugin.Data;

public class Transaction
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string ShopName { get; set; }
    public string StoreId { get; set; }
    public string OrderId { get; set; }
    public string InvoiceId { get; set; }
    public string InvoiceStatus { get; set; }
    public TransactionStatus TransactionStatus { get; set; }


    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

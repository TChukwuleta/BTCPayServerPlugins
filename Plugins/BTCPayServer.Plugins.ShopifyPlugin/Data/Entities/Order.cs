using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.ShopifyPlugin.Data;

public class Order
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string ShopName { get; set; }
    public string StoreId { get; set; }
    public string OrderId { get; set; }
    public string FinancialStatus { get; set; }
    public string CheckoutId { get; set; }
    public string CheckoutToken { get; set; }
    public string OrderNumber { get; set; }


    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

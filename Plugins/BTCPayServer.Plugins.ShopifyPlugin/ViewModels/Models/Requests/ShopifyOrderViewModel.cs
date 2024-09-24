namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;

public class ShopifyOrderViewModel
{
    public string OrderId { get; set; }
    public string InvoiceId { get; set; }
    public string ShopName { get; set; }
    public string BTCPayServerUrl { get; set; }
}

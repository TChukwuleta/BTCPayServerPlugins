namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels;

public class CreateShopifyOrderRequest
{
    public string storeId { get; set; }
    public string orderId { get; set; }
    public string currency { get; set; }
    public decimal total { get; set; }
    public string email { get; set; }
}

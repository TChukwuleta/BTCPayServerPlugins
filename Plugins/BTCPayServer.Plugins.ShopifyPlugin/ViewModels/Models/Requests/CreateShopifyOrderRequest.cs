namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels;

public class CreateShopifyOrderRequest
{
    public string shopName { get; set; }
    public string checkoutToken { get; set; }
    public string currency { get; set; }
    public decimal total { get; set; }
    public string email { get; set; }
}

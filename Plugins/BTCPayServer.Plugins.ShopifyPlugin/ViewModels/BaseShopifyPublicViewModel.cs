using BTCPayServer.Models;

namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels;

public class BaseShopifyPublicViewModel
{
    // store properties
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}

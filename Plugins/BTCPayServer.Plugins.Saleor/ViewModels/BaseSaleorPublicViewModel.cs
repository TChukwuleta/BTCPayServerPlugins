using BTCPayServer.Models;

namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class BaseSaleorPublicViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}

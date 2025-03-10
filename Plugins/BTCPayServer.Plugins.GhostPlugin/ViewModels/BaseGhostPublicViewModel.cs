using BTCPayServer.Models;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class BaseGhostPublicViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}

public class GhostOrderViewModel : BaseGhostPublicViewModel
{
    public string InvoiceId { get; set; }
    public string BTCPayServerUrl { get; set; }
}

using BTCPayServer.Models;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class BaseNairaPublicViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}

public class NairaOrderViewModel : BaseNairaPublicViewModel
{
    public string InvoiceId { get; set; }
    public string BTCPayServerUrl { get; set; }
}

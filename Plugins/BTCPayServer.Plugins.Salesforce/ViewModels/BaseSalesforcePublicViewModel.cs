using BTCPayServer.Models;

namespace BTCPayServer.Plugins.Salesforce.ViewModels;

public class BaseSalesforcePublicViewModel
{
    // store properties
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}

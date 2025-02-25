using BTCPayServer.Models;

namespace BTCPayServer.Plugins.SimpleTicketSales.ViewModels;

public class BaseGhostPublicViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}


public class SimpleTicketSalesOrderViewModel : BaseGhostPublicViewModel
{
    public string InvoiceId { get; set; }
    public string BTCPayServerUrl { get; set; }
    public string RedirectUrl { get; set; }
}

using BTCPayServer.Models;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels;

public class BaseSimpleTicketPublicViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public StoreBrandingViewModel StoreBranding { get; set; }
}


public class SimpleTicketSalesOrderViewModel : BaseSimpleTicketPublicViewModel
{
    public string InvoiceId { get; set; }
    public string BTCPayServerUrl { get; set; }
}

using System.Collections.Generic;

namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class CreateBigCommerceStoreRequest
{
    public string storeId { get; set; }
    public string cartId { get; set; }
    public string currency { get; set; }
    public decimal total { get; set; }
    public string email { get; set; }
}

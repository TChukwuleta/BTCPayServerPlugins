namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class BigCommerceOrderDetails
{
    public long id { get; set; }
    public string total_inc_tax { get; set; }
    public string currency_code { get; set; }
    public int status_id { get; set; }
}

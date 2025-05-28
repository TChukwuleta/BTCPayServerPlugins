namespace BTCPayServer.Plugins.Salesforce.ViewModels;

public class CreateInvoiceRequestVm
{
    public decimal amount { get; set; }
    public string currency { get; set; } = "USD";
    public string orderId { get; set; }
    public string buyerEmail { get; set; }
    public string orderType { get; set; }
    public string redirectUrl { get; set; }
}

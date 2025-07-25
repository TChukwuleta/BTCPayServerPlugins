namespace BTCPayServer.Plugins.Salesforce.ViewModels;

public class CreateInvoiceRequestVm
{
    public string amount { get; set; }
    public string currency { get; set; }
    public string orderId { get; set; }
    public string checkoutId { get; set; }
    public string cartId { get; set; }
    public string webstoreId { get; set; }
}

namespace BTCPayServer.Plugins.Salesforce.ViewModels;

public class CreateInvoiceRequestVm
{
    public decimal amount { get; set; }
    public string currency { get; set; }
    public string orderId { get; set; }
    public string paymentMethodId { get; set; }
    public object metadata { get; set; }
}

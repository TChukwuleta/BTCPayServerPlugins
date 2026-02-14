namespace BTCPayServer.Plugins.Saleor.ViewModels;

public class CreateInvoiceViewModel
{
    public string Amount { get; set; }
    public string Currency { get; set; }
    public string RedirectUrl { get; set; }
    public object MetaData { get; set; }
    public string TransactionId { get; set; }
}

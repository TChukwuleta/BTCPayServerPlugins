namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class CreateBigCommerceOrderResponse
{
    public BigcommerceOrderData data { get; set; }
    public CheckoutMetaData meta { get; set; }
}

public class BigcommerceOrderData
{
    public int id { get; set; }
}

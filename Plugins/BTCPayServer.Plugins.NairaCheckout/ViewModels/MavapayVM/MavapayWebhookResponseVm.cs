using System;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

internal class MavapayWebhookResponseVm
{
    public string @event { get; set; }
    public WebhookData data { get; set; }
}

public class WebhookData
{
    public string id { get; set; }
    public string walletId { get; set; }
    public string @ref { get; set; }
    public string hash { get; set; }
    public int amount { get; set; }
    public int fees { get; set; }
    public string currency { get; set; }
    public string type { get; set; }
    public string status { get; set; }
    public bool autopayout { get; set; }
    public DateTime? createdAt { get; set; }
    public DateTime? updatedAt { get; set; }
    public object btcUsdMetadata { get; set; }
}

public class BtcUsdMetadata
{
    public string id { get; set; }
    public string orderId { get; set; }
    public string paymentMethod { get; set; }
    public object externalRef { get; set; }
    public int customerInternalFee { get; set; }
    public int estimatedRoutingFee { get; set; }
    public object onChainAddress { get; set; }
    public string lnInvoice { get; set; }
    public object lnAddress { get; set; }
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
}
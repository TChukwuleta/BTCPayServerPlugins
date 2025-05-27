using System;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class CreateWebhookResponseVm
{
    public string id { get; set; }
    public string url { get; set; }
    public bool isActive { get; set; }
    public DateTime createdAt { get; set; }
}

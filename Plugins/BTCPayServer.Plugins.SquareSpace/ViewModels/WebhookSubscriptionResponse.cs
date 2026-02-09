namespace BTCPayServer.Plugins.SquareSpace.ViewModels;

public class WebhookSubscriptionResponse
{
    public bool Success { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Secret { get; set; }
    public string? Error { get; set; }
}

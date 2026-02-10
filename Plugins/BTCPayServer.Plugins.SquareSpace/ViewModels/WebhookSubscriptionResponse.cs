namespace BTCPayServer.Plugins.SquareSpace.ViewModels;

public class WebhookSubscriptionResponse
{
    public string? SubscriptionId { get; set; }
    public string? Secret { get; set; }
}


public class GenericResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
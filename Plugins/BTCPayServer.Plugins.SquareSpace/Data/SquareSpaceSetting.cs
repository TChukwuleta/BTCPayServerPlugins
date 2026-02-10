using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SquareSpace.Data;

public class SquareSpaceSetting
{
    [Key]
    public string Id { get; set; }
    public string OAuthToken { get; set; }
    public string WebsiteId { get; set; }
    public string WebhookEndpointUrl { get; set; }
    public string WebhookSecret { get; set; }
    public string WebhookSubscriptionId { get; set; }
    public bool AutoCreateInvoices { get; set; } = true;
    public string StoreId { get; set; }
}

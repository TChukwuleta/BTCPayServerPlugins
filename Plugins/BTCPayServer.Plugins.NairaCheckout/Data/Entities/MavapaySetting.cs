using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.NairaCheckout.Data;

public class MavapaySetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Display(Name = "Mavapay API Key")]
    public string ApiKey { get; set; }
    public string StoreId { get; set; }
    public string WebhookSecret { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }
    public DateTimeOffset? IntegratedAt { get; set; }
}

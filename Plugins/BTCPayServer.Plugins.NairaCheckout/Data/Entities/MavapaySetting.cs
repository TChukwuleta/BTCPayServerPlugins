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


public class MavapayCheckoutSettings
{
    public bool EnableSplitPayment { get; set; }
    public int SplitPercentage { get; set; }
    public string Currency { get; set; }
    public string NGNBankCode { get; set; }
    public string NGNAccountNumber { get; set; }
    public string NGNBankName { get; set; }
    public string NGNAccountName { get; set; }
    public string KESMethod { get; set; }
    public string KESAccountNumber { get; set; }
    public string KESIdentifier { get; set; }
    public string KESAccountName { get; set; }
    public string ZARBank { get; set; }
    public string ZARAccountNumber { get; set; }
    public string ZARAccountName { get; set; }
}
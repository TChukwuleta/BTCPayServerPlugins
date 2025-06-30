using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.Salesforce.Data;

public class SalesforceSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    [Display(Name = "Consumer Key")]
    public string ConsumerKey { get; set; }
    [Display(Name = "Consumer Secret")]
    public string ConsumerSecret { get; set; }

    [Display(Name = "Salesforce security token")]
    public string SecurityToken { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }
    public string PaymentGatewayProvider { get; set; }
    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(ConsumerKey) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ConsumerSecret);
    }

    public DateTimeOffset? IntegratedAt { get; set; }
    [NotMapped]
    public bool HasWallet { get; set; } = true;

    [NotMapped]
    public string CryptoCode { get; set; }
}
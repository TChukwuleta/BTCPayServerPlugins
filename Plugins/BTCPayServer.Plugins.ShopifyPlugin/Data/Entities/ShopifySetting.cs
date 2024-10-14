using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.ShopifyPlugin.Data;

public class ShopifySetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Display(Name = "Shop Name")]
    public string ShopName { get; set; }

    [Display(Name = "API Key")]
    public string ApiKey { get; set; }

    [Display(Name = "Admin API access token")]
    public string Password { get; set; }

    [Display(Name = "API Secret")]
    public string ApiSecret { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string WebhookId { get; set; }
    public string ApplicationUserId { get; set; }

    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(ShopName) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ApiSecret);
    }

    public DateTimeOffset? IntegratedAt { get; set; }

    [JsonIgnore]
    public string ShopifyUrl
    {
        get
        {
            return ShopName?.Contains('.', StringComparison.OrdinalIgnoreCase) is true ? ShopName : $"https://{ShopName}.myshopify.com";
        }
    }

    [NotMapped]
    public bool HasWallet { get; set; } = true;

    [NotMapped]
    public string CryptoCode { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
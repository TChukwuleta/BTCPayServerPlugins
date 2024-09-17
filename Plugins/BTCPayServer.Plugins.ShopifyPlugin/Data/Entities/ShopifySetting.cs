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
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }

    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(ShopName) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Password);
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
    public bool HasStore { get; set; } = true;

    [NotMapped]
    public string CryptoCode { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
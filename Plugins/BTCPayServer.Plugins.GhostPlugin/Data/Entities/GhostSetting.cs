using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public class GhostSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Display(Name = "Ghost API URL")]
    public string ShopName { get; set; } // https://tobses-1.ghost.io

    [Display(Name = "Ghost Admin Domain")]
    public string AdminDomain { get; set; }

    [Display(Name = "Admin API Key")]
    public string AdminApiKey { get; set; }

    [Display(Name = "Content API Key")]
    public string ContentApiKey { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }
    public DateTimeOffset? IntegratedAt { get; set; }
    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(ShopName) &&
            !string.IsNullOrWhiteSpace(AdminApiKey) &&
            !string.IsNullOrWhiteSpace(ContentApiKey);
    }

    [NotMapped]
    public bool HasWallet { get; set; } = true;

    [NotMapped]
    public string CryptoCode { get; set; }
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
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
    public string ApiUrl { get; set; }

    [Display(Name = "Admin API Key")]
    public string AdminApiKey { get; set; }

    [Display(Name = "Content API Key")]
    public string ContentApiKey { get; set; }
    public DateTimeOffset? IntegratedAt { get; set; }
    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(ApiUrl) &&
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
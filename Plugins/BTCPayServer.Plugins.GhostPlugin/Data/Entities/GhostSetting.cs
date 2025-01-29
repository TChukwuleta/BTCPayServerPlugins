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

    [Display(Name = "Ghost Username/Email")]
    public string Username { get; set; }
    public string WebhookSecret { get; set; }
    public string Password { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }
    public DateTimeOffset? IntegratedAt { get; set; }
    public bool CredentialsPopulated()
    {
        return
            !string.IsNullOrWhiteSpace(ApiUrl) &&
            !string.IsNullOrWhiteSpace(AdminApiKey) &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);
    }

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}
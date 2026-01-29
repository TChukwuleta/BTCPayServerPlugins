using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.StoreBridge.Data;

public class TemplateData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string Description { get; set; }
    public string Tags { get; set; }
    public string Version { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Category { get; set; }

    [Required]
    public string UploadedBy { get; set; }

    [Required]
    public DateTimeOffset UploadedAt { get; set; }

    [Required]
    public byte[] FileData { get; set; } // The .storebridge file

    public int DownloadCount { get; set; }
    public string IncludedOptions { get; set; } // JSON array: ["Subscriptions", "CheckoutSettings"]
}

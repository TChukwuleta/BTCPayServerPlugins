using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.LightSpeed.Data;

public class LightspeedSettings
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string StoreId { get; set; } 
    public string LightspeedDomainPrefix { get; set; } 
    public string? LightspeedPersonalAccessToken { get; set; }
    public string Currency { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(StoreId) && !string.IsNullOrWhiteSpace(LightspeedDomainPrefix);
}

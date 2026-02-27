using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.JumpSeller.Data;

public class JumpSellerStoreSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public string EpgAccountId { get; set; }
    public string EpgSecret { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.NairaCheckout.Data;

public class NairaCheckoutSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public bool Enabled { get; set; }
    public string WalletName { get; set; }
}

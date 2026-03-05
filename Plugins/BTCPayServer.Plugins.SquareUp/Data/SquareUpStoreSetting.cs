using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SquareUp.Data;

public class SquareUpStoreSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string AccessToken { get; set; } 
    public string LocationId { get; set; } // The Square Location ID to associate payments with.
    public PayoutPreference PayoutPreference { get; set; } = PayoutPreference.Fiat;

    /// <summary>Whether to use Square's sandbox environment.</summary>
    public bool IsSandbox { get; set; }
    public string SquareWebhookSignatureKey { get; set; }
}
public enum PayoutPreference
{
    Fiat, // Customer pays BTC; merchant receives equivalent fiat in Square
    BTC // Customer pays BTC; merchant keeps BTC in associated BTCPay wallet
}
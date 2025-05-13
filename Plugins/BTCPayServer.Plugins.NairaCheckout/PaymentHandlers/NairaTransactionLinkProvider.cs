using BTCPayServer.Services;

namespace BTCPayServer.Plugins.Mavapay.PaymentHandlers;

internal class NairaTransactionLinkProvider(string blockExplorerLink) : DefaultTransactionLinkProvider(blockExplorerLink)
{
    public override string? GetTransactionLink(string paymentId)
    {
        return null;
    }
}

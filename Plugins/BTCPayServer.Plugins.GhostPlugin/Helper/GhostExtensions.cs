using BTCPayServer.Plugins.GhostPlugin.Data;
using BTCPayServer.Plugins.GhostPlugin.Services;

namespace BTCPayServer.Plugins.GhostPlugin.Helper;

public static class GhostExtensions
{
    public static GhostApiClientCredentials CreateGhsotApiCredentials(this GhostSetting ghost)
    {
        return new GhostApiClientCredentials
        {
            ShopName = ghost.ShopName,
            AdminApiKey = ghost.AdminApiKey
        };
    }
}

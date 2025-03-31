using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.Helper;

public static class GhostExtensions
{
    public static GhostApiClientCredentials CreateGhsotApiCredentials(this GhostSetting ghost)
    {
        return new GhostApiClientCredentials
        {
            ApiUrl = ghost.ApiUrl,
            AdminApiKey = ghost.AdminApiKey,
            ContentApiKey = ghost.ContentApiKey
        };
    }
}

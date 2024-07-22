
namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class BigCommerceSignedJwtPayloadRequest
{
    public string aud { get; set; } // your app's CLIENT_ID
    public string iss { get; set; }
    public long iat { get; set; }
    public long nbf { get; set; }
    public long exp { get; set; }
    public string jti { get; set; } // JWT unique identifier
    public string sub { get; set; } // STORE_HASH
    public object user { get; set; }
    public object owner { get; set; }
    public string url { get; set; }
    public object channel_id { get; set; }
}
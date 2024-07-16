
namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class BigCommerceSignedJwtPayloadRequest
{
    public string aud { get; set; } // your app's CLIENT_ID
    public string iss { get; set; }
    public int iat { get; set; }
    public int nbf { get; set; }
    public int exp { get; set; }
    public string jti { get; set; } // JWT unique identifier
    public string sub { get; set; } // STORE_HASH
    public JwtPayloadUser user { get; set; }
    public JwtPayloadOwner owner { get; set; }
    public string url { get; set; }
    public object channel_id { get; set; }
}


public class JwtPayloadOwner
{
    public int id { get; set; }
    public string email { get; set; }
}

public class JwtPayloadUser
{
    public int id { get; set; }
    public string email { get; set; }
    public string locale { get; set; }
}
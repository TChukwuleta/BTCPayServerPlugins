namespace BTCPayServer.Plugins.BigCommercePlugin.ViewModels;

public class InstallBigCommerceApplicationRequestModel
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RedirectUrl { get; set; } // Use ngrok to test
    public string Code { get; set; }
    public string Scope { get; set; }
    public string Context { get; set; }
    public string GrantType { get; set; } = "authorization_code";
}

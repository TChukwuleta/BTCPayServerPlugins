using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.BigCommercePlugin.Data;

public class BigCommerceStore
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RedirectUrl { get; set; }
    public string AccessToken { get; set; }
    public string Scope { get; set; }
    public string StoreHash { get; set; }
    public string BigCommerceUserId { get; set; }
    public string BigCommerceUserEmail { get; set; }
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public string ApplicationUserId { get; set; }
}

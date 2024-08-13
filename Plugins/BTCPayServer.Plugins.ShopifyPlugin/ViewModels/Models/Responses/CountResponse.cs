using Newtonsoft.Json;

namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;

public class CountResponse
{
    [JsonProperty("count")]
    public long Count { get; set; }
}

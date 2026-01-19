using Newtonsoft.Json;

namespace BTCPayServer.Plugins.NairaCheckout.ViewModels;

public class MavapayRateResponse
{
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("data")]
    public MavapayRateData Data { get; set; }
}

public class MavapayRateData
{
    [JsonProperty("ask")]
    public decimal Ask { get; set; }

    [JsonProperty("bid")]
    public decimal Bid { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("meta")]
    public object Meta { get; set; }
}
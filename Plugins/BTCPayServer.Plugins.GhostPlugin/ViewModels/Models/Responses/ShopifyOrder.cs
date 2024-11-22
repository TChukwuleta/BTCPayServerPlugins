using Newtonsoft.Json;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;

public class ShopifyOrder
{
    [JsonProperty("id")]
    public long Id { get; set; }
    [JsonProperty("order_number")]
    public long OrderNumber { get; set; }
    [JsonProperty("total_price")]
    public decimal TotalPrice { get; set; }
    [JsonProperty("total_outstanding")]
    public decimal TotalOutstanding { get; set; }
    [JsonProperty("currency")]
    public string Currency { get; set; }
    [JsonProperty("presentment_currency")]
    public string PresentmentCurrency { get; set; }
    [JsonProperty("financial_status")]
    public string FinancialStatus { get; set; }
    [JsonProperty("transactions")]
    public IEnumerable<ShopifyTransaction> Transactions { get; set; }
}

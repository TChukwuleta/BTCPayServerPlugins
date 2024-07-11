using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.MassStoreGenerator.Helper
{
    public class StoreBlobHelper
    { 
        public static string StandardDefaultCurrency = "USD";
        public static string StandardDefaultExchange = "coingecko";

        public StoreBlobHelper()
        {
        }
        string _DefaultCurrency;
        public string DefaultCurrency
        {
            get
            {
                return string.IsNullOrEmpty(_DefaultCurrency) ? StandardDefaultCurrency : _DefaultCurrency;
            }
            set
            {
                _DefaultCurrency = value;
                if (!string.IsNullOrEmpty(_DefaultCurrency))
                    _DefaultCurrency = _DefaultCurrency.Trim().ToUpperInvariant();
            }
        }

        public static JObject RecommendedExchanges { get; set; } = new()
        {
            { "EUR", "kraken" },
            { "USD", "kraken" },
            { "GBP", "kraken" },
            { "CHF", "kraken" },
            { "GTQ", "bitpay" },
            { "COP", "yadio" },
            { "ARS", "yadio" },
            { "JPY", "bitbank" },
            { "TRY", "btcturk" },
            { "UGX", "yadio"},
            { "RSD", "bitpay"},
            { "NGN", "bitnob"}
        };

        public string GetRecommendedExchange() =>
            RecommendedExchanges.Property(DefaultCurrency)?.Value.ToString() ?? "coingecko";
    }
}

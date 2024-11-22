using System;

namespace BTCPayServer.Plugins.ShopifyPlugin
{
    public class ShopifyApiException : Exception
    {
        public ShopifyApiException(string message) : base(message)
        {
        }
    }
}

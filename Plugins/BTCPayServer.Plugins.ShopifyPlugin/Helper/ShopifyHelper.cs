using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.ShopifyPlugin.Helper;

public class ShopifyHelper
{

    public async Task<(bool succeeded, string response)> GetCustomJavascript(string storeId, string baseUrl)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var bcStore = await ctx.BigCommerceStores.SingleOrDefaultAsync(c => c.StoreId == storeId);

        if (bcStore == null)
        {
            return (false, "Invalid store Id specified");
        }

        string[] fileUrls = new[]
        {
            "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.BigCommercePlugin/Resources/js/btcpay.js",
            "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.BigCommercePlugin/Resources/js/btcpay-shopify.js"
        };

        StringBuilder combinedJavascript = new StringBuilder();

        using (var httpClient = new HttpClient())
        {
            foreach (var fileUrl in fileUrls)
            {
                try
                {
                    string fileContent = await httpClient.GetStringAsync(fileUrl);
                    combinedJavascript.AppendLine(fileContent);
                }
                catch (HttpRequestException httpEx)
                {
                    return (false, $"Failed to fetch file content due to a network issue: {httpEx.Message}");
                }
                catch (Exception ex)
                {
                    return (false, $"Failed to fetch file content: {ex.Message}");
                }
            }
        }
        string jsVariables = $"var BTCPAYSERVER_URL = '{baseUrl}'; var STORE_HASH = '{bcStore.StoreHash}'; var BTCPAYSERVER_STORE_ID = '{storeId}';";
        combinedJavascript.Insert(0, jsVariables + Environment.NewLine);

        return (true, combinedJavascript.ToString());
    }
}

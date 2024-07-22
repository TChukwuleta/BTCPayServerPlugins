using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Helper
{
    public class BigCommerceHelper
    {
        private readonly BigCommerceService _bigCommerceService;
        private readonly BigCommerceDbContextFactory _dbContextFactory;
        public BigCommerceHelper(BigCommerceService bigCommerceService, BigCommerceDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            _bigCommerceService = bigCommerceService;

        }

        public BigCommerceSignedJwtPayloadRequest DecodeJwtPayload(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var payload = jwtToken.Payload;
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            var payloadData = System.Text.Json.JsonSerializer.Deserialize<BigCommerceSignedJwtPayloadRequest>(payloadJson);
            return payloadData;
        }

        public bool ValidateClaims(BigCommerceStore store, dynamic claims)
        {
            return store.StoreHash == claims.sub && store.ClientId == claims.aud;
        }

        public async Task<BigCommerceStore> UploadCheckoutScript(BigCommerceStore bigCommerceStore, string jsFilePath)
        {
            CreateCheckoutScriptResponse script = null;
            if (!string.IsNullOrEmpty(bigCommerceStore.JsFileUuid))
            {
                var existingScript = await _bigCommerceService.GetCheckoutScriptAsync(bigCommerceStore.JsFileUuid, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                if (existingScript == null)
                {
                    script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, jsFilePath, bigCommerceStore.AccessToken);
                }
            }
            else
            {
                script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, jsFilePath, bigCommerceStore.AccessToken);
            }
            if (script != null && !string.IsNullOrEmpty(script.data.uuid))
            {
                bigCommerceStore.JsFileUuid = script.data.uuid;
            }
            return bigCommerceStore;
        }

        public async Task<(bool succeeded, string response)> GetCustomJavascript(string storeId, string baseUrl)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var bcStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
            if (bcStore == null)
            {
                return (false, "Invalid store Id specified");
            }

            string fileUrl = "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.BigCommercePlugin/Resources/js/btcpay-bc.js";

            string combinedJavascript = string.Empty;

            using (var httpClient = new HttpClient())
            {
                try
                {
                    combinedJavascript = await httpClient.GetStringAsync(fileUrl);
                }
                catch (Exception ex)
                {
                    return (false, $"Failed to fetch file content: {ex.Message}");
                }
            }

            // Find a way to pull the JS file directly from the plugin through BTCPay server

            /*string resourcesFolder = Path.Combine(AppContext.BaseDirectory, "Resources", "js");
            string[] fileList = new[] { "btcpay-bc.js" };
            string combinedJavascript = string.Empty;

            foreach (var file in fileList)
            {
                string filePath = Path.Combine(resourcesFolder, file);
                _logger.LogInformation($"File path is: {filePath}");
                var fileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(filePath);
                if (fileInfo.Exists);
                {
                    await using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    combinedJavascript += Environment.NewLine + await reader.ReadToEndAsync();
                }
            }*/


            string jsVariables = $"var BTCPAYSERVER_URL = '{baseUrl}'; var STORE_HASH = '{bcStore.StoreHash}'; var BTCPAYSERVER_STORE_ID = '{storeId}';";
            return (true, $"{jsVariables}{combinedJavascript}");
        }
    }
}

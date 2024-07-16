using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Helper
{
    public class BigCommerceHelper
    {
        private readonly BigCommerceService _bigCommerceService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly BigCommerceDbContextFactory _dbContextFactory;
        public BigCommerceHelper(BigCommerceService bigCommerceService, IWebHostEnvironment webHostEnvironment, BigCommerceDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            _webHostEnvironment = webHostEnvironment;
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

        public async Task UploadCheckoutScript(BigCommerceStore bigCommerceStore)
        {
            CreateCheckoutScriptResponse script = null;
            if (!string.IsNullOrEmpty(bigCommerceStore.JsFileUuid))
            {
                var existingScript = await _bigCommerceService.GetCheckoutScriptAsync(bigCommerceStore.JsFileUuid, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                if (existingScript == null)
                {
                    script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, bigCommerceStore.StoreId);
                }
            }
            else
            {
                script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, bigCommerceStore.StoreId);
            }
            if (script != null && !string.IsNullOrEmpty(script.data.uuid))
            {
                bigCommerceStore.JsFileUuid = script.data.uuid;
            }
        }

        public async Task<string> GetCustomJavascript(string storeId, string baseUrl)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var bcStore = ctx.BigCommerceStores.FirstOrDefault(c => c.StoreId == storeId);
            if (bcStore == null)
            {
                // throw an error.
            }
            string[] fileList = new[] { "Resources/js/btcpay-bc.js" };
            string combinedJavascript = string.Empty;
            foreach (var file in fileList)
            {
                var fileInfo = _webHostEnvironment.WebRootFileProvider.GetFileInfo(file);
                if (fileInfo.Exists)
                {
                    await using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    combinedJavascript += Environment.NewLine + await reader.ReadToEndAsync();
                }
            }
            string jsVariables = $"var BTCPAYSERVER_URL = '{baseUrl}'; var STORE_HASH = '{bcStore.StoreHash}'; var BTCPAYSERVER_STORE_ID = '{storeId}';";
            return $"{jsVariables}{combinedJavascript}";
        }
    }
}

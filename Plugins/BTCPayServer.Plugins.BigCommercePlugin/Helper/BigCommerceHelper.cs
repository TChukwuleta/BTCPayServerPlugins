using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Helper
{
    public class BigCommerceHelper
    {
        private readonly HttpClient _client;
        private readonly BigCommerceService _bigCommerceService;
        private readonly BigCommerceDbContextFactory _dbContextFactory;
        public BigCommerceHelper(HttpClient client, BigCommerceService bigCommerceService, BigCommerceDbContextFactory dbContextFactory)
        {
            _client = client;
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
            if (!string.IsNullOrEmpty(bigCommerceStore.JsFileUuid))
            {
                var existingScript = await _bigCommerceService.GetCheckoutScriptAsync(bigCommerceStore.JsFileUuid, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                if (existingScript != null) return bigCommerceStore;
            }
            var script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, jsFilePath, bigCommerceStore.AccessToken);
            if (script?.data?.uuid != null)
            {
                bigCommerceStore.JsFileUuid = script.data.uuid;
            }
            return bigCommerceStore;
        }

        public string GetEmbeddedResourceContent(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = assembly.GetManifestResourceNames()
                                           .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullResourceName == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly.");
            }
            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}

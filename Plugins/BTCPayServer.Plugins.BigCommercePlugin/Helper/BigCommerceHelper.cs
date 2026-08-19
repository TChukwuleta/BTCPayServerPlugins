using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

        public BigCommerceSignedJwtPayloadRequest ValidateAndDecodeJwt(string token, BigCommerceStore store)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(store?.ClientId) || string.IsNullOrEmpty(store?.ClientSecret)) 
                return null;

            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(store.ClientSecret)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ValidateIssuer = true,
                ValidIssuer = "bc",
                ValidateAudience = true,
                ValidAudience = store.ClientId,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
            try
            {
                handler.ValidateToken(token, validationParameters, out var validatedToken);
                var jwtToken = (JwtSecurityToken)validatedToken;
                var payloadJson = JsonSerializer.Serialize(jwtToken.Payload);
                var payloadData = JsonSerializer.Deserialize<BigCommerceSignedJwtPayloadRequest>(payloadJson);
                return payloadData?.sub == store.StoreHash ? payloadData : null;
            }
            catch { return null; }
        }

        public BigCommerceSignedJwtPayloadRequest DecodeJwtPayload(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var payload = jwtToken.Payload;
            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadData = JsonSerializer.Deserialize<BigCommerceSignedJwtPayloadRequest>(payloadJson);
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
                var existingScript = await _bigCommerceService.GetCheckoutScript(bigCommerceStore.JsFileUuid, bigCommerceStore.StoreHash, bigCommerceStore.AccessToken);
                if (existingScript != null) return bigCommerceStore;
            }
            var script = await _bigCommerceService.SetCheckoutScript(bigCommerceStore.StoreHash, jsFilePath, bigCommerceStore.AccessToken);
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

using BTCPayServer.Plugins.BigCommercePlugin.Data;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.BigCommercePlugin.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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

        public string EncodePayload(BigCommerceSignedJwtPayloadRequest session, string clientSecret)
        {
            var context = session.sub.Split('/')[1] ?? string.Empty;

            var claims = new[]
            {
                new System.Security.Claims.Claim("context", context),
                new System.Security.Claims.Claim("user", session.user.ToString()),
                new System.Security.Claims.Claim("owner", session.owner.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clientSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddHours(24),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string BuildRedirectUrl(string url, string encodedContext)
        {
            var uri = new Uri(url);
            var query = uri.Query.TrimStart('?');
            var queryParams = HttpUtility.ParseQueryString(query);
            queryParams["context"] = encodedContext;

            var newQuery = queryParams.ToString();
            return $"{uri.GetLeftPart(UriPartial.Path)}?{newQuery}";
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
                if (existingScript != null)
                {
                    return bigCommerceStore;
                }
            }
            var script = await _bigCommerceService.SetCheckoutScriptAsync(bigCommerceStore.StoreHash, jsFilePath, bigCommerceStore.AccessToken);
            if (script?.data?.uuid != null)
            {
                bigCommerceStore.JsFileUuid = script.data.uuid;
            }
            return bigCommerceStore;
        }

        public async Task<GenericResponse> GetBtcpayCustomJavascriptModal(string storeId)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var bcStore = await ctx.BigCommerceStores.FirstOrDefaultAsync(c => c.StoreId == storeId);
            if (bcStore == null)
            {
                return new GenericResponse { Success = false,  Content = $"Invalid store Id specified" };
            }
            string fileUrl = "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.BigCommercePlugin/Resources/js/btcpay.js";
            try
            {
                var combinedJavascript = await _client.GetStringAsync(fileUrl);
                return new GenericResponse { Success = true, Content = combinedJavascript };
            }
            catch (HttpRequestException ex)
            {
                return new GenericResponse { Success = false, Content = $"Failed to fetch file content: {ex.Message}" };
            }
        }


        public async Task<GenericResponse> GetCustomJavascript(string storeId, string baseUrl)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            var bcStore = await ctx.BigCommerceStores.FirstOrDefaultAsync(c => c.StoreId == storeId);
            if (bcStore == null)
            {
                return new GenericResponse { Success = false, Content = "Invalid store Id specified" };
            }
            string fileUrl = "https://raw.githubusercontent.com/TChukwuleta/BTCPayServerPlugins/main/Plugins/BTCPayServer.Plugins.BigCommercePlugin/Resources/js/btcpay-bc.js";
            string combinedJavascript;
            try
            {
                combinedJavascript = await _client.GetStringAsync(fileUrl);
            }
            catch (HttpRequestException ex)
            {
                return new GenericResponse { Success = false, Content = $"Failed to fetch file content: {ex.Message}" };
            }
            /*string jsVariables = $"var BTCPAYSERVER_URL = '{baseUrl}'; var STORE_HASH = '{bcStore.StoreHash}'; var BTCPAYSERVER_STORE_ID = '{storeId}';";
            $"{jsVariables}{combinedJavascript}";*/
            var jsBuilder = new StringBuilder();
            jsBuilder.Append($"var BTCPAYSERVER_URL = '{baseUrl}'; ");
            jsBuilder.Append($"var STORE_HASH = '{bcStore.StoreHash}'; ");
            jsBuilder.Append($"var BTCPAYSERVER_STORE_ID = '{storeId}'; ");
            jsBuilder.Append(combinedJavascript);
            return new GenericResponse { Success = true, Content = jsBuilder.ToString() };
        }
    }
}

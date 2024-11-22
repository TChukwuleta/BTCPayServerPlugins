using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostApiClient
    {
        private readonly HttpClient _httpClient;

        public GhostApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<bool> ValidateGhostCredentials(string adminApiKey, string apiUrl)
        {
            var jwt = GenerateGhostJWT(adminApiKey, apiUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Ghost", jwt);
            var response = await _httpClient.GetAsync($"{apiUrl}/ghost/api/admin/site/");
            return response.IsSuccessStatusCode;
        }

        private string GenerateGhostJWT(string adminApiKey, string apiUrl)
        {
            var keyParts = adminApiKey.Split(':');
            var keyId = keyParts[0];
            var secret = Convert.FromBase64String(keyParts[1]);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = keyId,
                Audience = apiUrl,
                Expires = DateTime.UtcNow.AddMinutes(5),
                Claims = new Dictionary<string, object>(),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private HttpRequestMessage CreateRequest(string shopName, HttpMethod method, string action,
            string relativeUrl = null, string apiVersion = "2024-07")
        {
            var url =
                $"https://{(shopName.Contains('.', StringComparison.InvariantCulture) ? shopName : $"{shopName}.myshopify.com")}/{relativeUrl ?? ($"admin/api/{apiVersion}/" + action)}";
            var req = new HttpRequestMessage(method, url);
            return req;
        }
    }

    public class ShopifyApiClientCredentials
    {
        public string ShopName { get; set; }
        public string ApiKey { get; set; }
        public string ApiPassword { get; set; }
    }
}


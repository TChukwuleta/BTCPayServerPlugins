using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GhostApiClientCredentials _credentials;
        public GhostApiClient(IHttpClientFactory httpClientFactory, GhostApiClientCredentials credentials)
        {
            _credentials = credentials;
            if (httpClientFactory != null)
            {
                _httpClient = httpClientFactory.CreateClient(nameof(GhostApiClient));
            }
            else
            {
                _httpClient = new HttpClient();
            }
        }

        public async Task<bool> ValidateGhostCredentials()
        {
            var jwt = GenerateGhostApiToken();
            var url = $"https://{(_credentials.ShopName.Contains('.', StringComparison.InvariantCulture) ? $"{_credentials.ShopName}/ghost/api/admin/site" : $"{_credentials.ShopName}.ghost.io")}/ghost/api/admin/site";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Ghost", jwt);
            var response = await _httpClient.GetAsync(url);
            Console.WriteLine(response.Content);
            return response.IsSuccessStatusCode;
        }

        public async Task<string> RetrieveGhostTiers()
        {
            var req = CreateRequest(_credentials.ShopName, HttpMethod.Get, "tiers?include=monthly_price,yearly_price,benefits");
            var tiers = await SendRequest(req);
            return tiers;
        }

        public async Task CreateGhostMember(string name, string email)
        {
            var req = CreateRequest(_credentials.ShopName, HttpMethod.Get, "tiers?include=monthly_price,yearly_price,benefits");
            var tiers = await SendRequest(req);
        }

        private string GenerateGhostApiToken()
        {
            // In accordance to this docs: https://ghost.org/docs/admin-api/#token-authentication
            string[] keyParts = _credentials.AdminApiKey.Split(':');
            string id = keyParts[0];
            string secret = keyParts[1];
            var securityKey = new SymmetricSecurityKey(HexStringToByteArray(secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var header = new JwtHeader(credentials)
            {
                { "kid", id }
            };
            var payload = new JwtPayload
            {
                { "aud", "/admin/" },
                { "exp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5 * 60 },
                { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };
            var jwtToken = new JwtSecurityToken(header, payload);
            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(jwtToken);
        }

        byte[] HexStringToByteArray(string hex)
        {
            int length = hex.Length;
            byte[] bytes = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        private HttpRequestMessage CreateRequest(string shopName, HttpMethod method, string relativeUrl)
        {
            var url =
                $"https://{(shopName.Contains('.', StringComparison.InvariantCulture) ? $"{shopName}/ghost/api/admin/{relativeUrl}" : $"{shopName}.ghost.io")}/ghost/api/admin/{relativeUrl}";
            var req = new HttpRequestMessage(method, url);
            return req;
        }


        private async Task<string> SendRequest(HttpRequestMessage req)
        {
            string bearer = GenerateGhostApiToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Ghost", bearer);
            // _httpClient.DefaultRequestHeaders.Add("Authorization", "Ghost " + bearer);
            using var resp = await _httpClient.SendAsync(req);

            var strResp = await resp.Content.ReadAsStringAsync();
            /*if (strResp.StartsWith("{", StringComparison.OrdinalIgnoreCase) && JObject.Parse(strResp)["errors"]?.Value<string>() is string error)
            {
                if (error == "Not Found")
                    error = "Shop not found";
                throw new ShopifyApiException(error);
            }*/
            return strResp;
        }
    }

    public class GhostApiClientCredentials
    {
        public string ShopName { get; set; }
        public string AdminApiKey { get; set; }
    }
}


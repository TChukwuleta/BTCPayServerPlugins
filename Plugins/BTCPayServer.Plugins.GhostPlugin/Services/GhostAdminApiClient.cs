using System;
using JWT.Builder;
using System.Net;
using System.Text;
using JWT.Algorithms;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostAdminApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GhostApiClientCredentials _credentials;
        private readonly string ApiVersion = "v5.0"; 
        private readonly string sessionUrl = "https://tobses-1.ghost.io/ghost/api/admin/session"; // make it dynamic..
        public GhostAdminApiClient(IHttpClientFactory httpClientFactory, GhostApiClientCredentials credentials)
        {
            _credentials = credentials;
            if (httpClientFactory != null)
            {
                _httpClient = httpClientFactory.CreateClient(nameof(GhostAdminApiClient));
            }
            else
            {
                var handler = new HttpClientHandler
                {
                    UseCookies = true,
                    UseDefaultCredentials = true,
                    CookieContainer = new CookieContainer()
                };
                _httpClient = new HttpClient(handler);
            }
        }

        public async Task<bool> ValidateGhostCredentials()
        {

            var postData = new
            {
                username = _credentials.UserName,
                password = _credentials.Password 
            };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(postData), Encoding.UTF8, "application/json");
            var sessionResponse = await _httpClient.PostAsync(sessionUrl, jsonContent);
            if (sessionResponse.StatusCode != HttpStatusCode.Created)
                return false;
            var token = GhostAdminAPIToken();
            var url = $"https://{(_credentials.ShopName.Contains('.', StringComparison.InvariantCulture) ? $"{_credentials.ShopName}/ghost/api/admin/site" : $"{_credentials.ShopName}.ghost.io")}/ghost/api/admin/site";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Ghost", token);
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }

        public async Task<string> RetrieveGhostTiers()
        {
            Console.WriteLine(JsonConvert.SerializeObject(_credentials));
            var req = CreateRequest(HttpMethod.Get, "tiers?include=monthly_price,yearly_price,benefits");
            var tiers = await SendRequest(req);
            Console.WriteLine(tiers);
            return tiers;
        }

        public async Task<CreateMemberResponseModel> CreateGhostMember(CreateGhostMemberRequest requestModel)
        {
            var postJson = JsonConvert.SerializeObject(requestModel);
            var req = CreateRequest(HttpMethod.Post, "members");
            req.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
            var response = await SendRequest(req);
            return JsonConvert.DeserializeObject<CreateMemberResponseModel>(response);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
        {
            var url =
                $"https://{(_credentials.ShopName.Contains('.', StringComparison.InvariantCulture) ? $"{_credentials.ShopName}/ghost/api/admin/{relativeUrl}" : $"{_credentials.ShopName}.ghost.io")}/ghost/api/admin/{relativeUrl}";
            var req = new HttpRequestMessage(method, url);
            return req;
        }


        private async Task<string> SendRequest(HttpRequestMessage req)
        {
            string bearer = GhostAdminAPIToken();
            var postData = new
            {
                username = _credentials.UserName, // make it dynamic..
                password = _credentials.Password // make it dynamic..
            };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(postData), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync(sessionUrl, jsonContent);

            _httpClient.DefaultRequestHeaders.Add("Accept-Version", ApiVersion);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Ghost", bearer);
            req.Headers.Authorization = new AuthenticationHeaderValue("Ghost", bearer);
            var response = await _httpClient.SendAsync(req);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private string GhostAdminAPIToken()
        {
            // In accordance to this docs: https://ghost.org/docs/admin-api/#token-authentication
            var adminKeyParts = _credentials.AdminApiKey.Split(':');
            var id = adminKeyParts[0];
            var secret = adminKeyParts[1];
            var unixEpochInSeconds = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();

            var token = new JwtBuilder().WithAlgorithm(new HMACSHA256Algorithm())
                                        .WithSecret(Convert.FromHexString(secret))
                                        .AddHeader(HeaderName.KeyId, id)
                                        .AddHeader(HeaderName.Type, "JWT")
                                        .AddClaim("exp", unixEpochInSeconds + 300)
                                        .AddClaim("iat", unixEpochInSeconds)
                                        .AddClaim("aud", "/admin/")
                                        .Encode();
            return token;
        }

        private string GenerateGhostApiToken()
        {
            // In accordance to this docs: https://ghost.org/docs/admin-api/#token-authentication
            string[] keyParts = _credentials.AdminApiKey.Split(':');
            string id = keyParts[0];
            string secret = keyParts[1];
            var securityKey = new SymmetricSecurityKey(HexStringToByteArray(secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var header = new System.IdentityModel.Tokens.Jwt.JwtHeader(credentials)
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
    }
}


using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using BTCPayServer.Plugins.GhostPlugin.Helper;
using BTCPayServer.Plugins.GhostPlugin.ViewModels.Models;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.GhostPlugin.Services
{
    public class GhostContentApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly GhostApiClientCredentials _credentials;
        private readonly string ApiVersion = "v5.0";
        public GhostContentApiClient(IHttpClientFactory httpClientFactory, GhostApiClientCredentials credentials)
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

        public async Task<List<Tier>> RetrieveGhostTiers()
        {
            var req = CreateRequest(HttpMethod.Get, $"tiers/?key={_credentials.ContentApiKey}&include=benefits,monthly_price,yearly_price");
            var response = await SendRequest(req);
            var tiers = JsonConvert.DeserializeObject<GetTiersResponse>(response);
            return tiers.tiers;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
        {
            var url = $"https://{_credentials.ApiUrl}/ghost/api/content/{relativeUrl}";
            var req = new HttpRequestMessage(method, url);
            return req;
        }

        private async Task<string> SendRequest(HttpRequestMessage req)
        {
            _httpClient.DefaultRequestHeaders.Add("Accept-Version", ApiVersion);
            var response = await _httpClient.SendAsync(req);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
    }
}


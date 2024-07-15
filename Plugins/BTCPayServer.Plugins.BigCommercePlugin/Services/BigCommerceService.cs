using BTCPayServer.Plugins.BigCommercePlugin.Data.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.BigCommercePlugin.Services;

public class BigCommerceService
{
    private readonly HttpClient _client;
    private readonly ILogger<BigCommerceService> _logger;   
    public BigCommerceService(HttpClient client, ILogger<BigCommerceService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<(bool success, string content)> InstallApplication(InstallBigCommerceApplicationRequestModel requestModel)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://login.bigcommerce.com/oauth2/token");
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", requestModel.ClientId),
            new KeyValuePair<string, string>("client_secret", requestModel.ClientSecret),
            new KeyValuePair<string, string>("code", requestModel.Code),
            new KeyValuePair<string, string>("scope", requestModel.Scope),
            new KeyValuePair<string, string>("grant_type", requestModel.GrantType),
            new KeyValuePair<string, string>("redirect_uri", requestModel.RedirectUrl),
            new KeyValuePair<string, string>("context", requestModel.Context)
        });
        request.Content = content;
        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"An error occurred while trying to install a big commerce application: {response.ReasonPhrase}");
            return (false, response.ReasonPhrase);
        }
        return (true, await response.Content.ReadAsStringAsync());
    }
}

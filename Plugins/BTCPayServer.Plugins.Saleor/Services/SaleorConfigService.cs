using System;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Saleor.ViewModels;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor.Services;

public class SaleorConfigService
{
    private readonly SaleorAplService _apl;
    private readonly SaleorGraphQLService _graphql;
    private readonly ILogger<SaleorConfigService> _logger;

    public SaleorConfigService(
        SaleorAplService apl,
        SaleorGraphQLService graphql,
        ILogger<SaleorConfigService> logger)
    {
        _apl = apl;
        _graphql = graphql;
        _logger = logger;
    }
    public async Task<BtcpayConfig?> GetConfigAsync(string saleorApiUrl)
    {
        var authData = await _apl.GetAsync(saleorApiUrl);
        if (authData is null)
        {
            _logger.LogWarning("No APL entry for {Url}", saleorApiUrl);
            return null;
        }
        return await _graphql.GetConfigAsync(saleorApiUrl, authData.Token);
    }

    public async Task SetConfigAsync(string saleorApiUrl, BtcpayConfig config)
    {
        var authData = await _apl.GetAsync(saleorApiUrl);
        if (authData is null) throw new Exception($"Saleor instance not registered: {saleorApiUrl}");
        await _graphql.SetConfigAsync(saleorApiUrl, authData.Token, config);
    }

    public async Task DeleteConfigAsync(string saleorApiUrl)
    {
        var authData = await _apl.GetAsync(saleorApiUrl);
        if (authData is null) throw new Exception($"Saleor instance not registered: {saleorApiUrl}");
        await _graphql.DeleteConfigAsync(saleorApiUrl, authData.Token);
    }
}

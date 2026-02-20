using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Saleor;

public class SaleorPluginMigrationRunner
{
    private readonly ILogger<SaleorPluginMigrationRunner> _logger;

    public SaleorPluginMigrationRunner(ILogger<SaleorPluginMigrationRunner> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BTCPay Saleor Plugin started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

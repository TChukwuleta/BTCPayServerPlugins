using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Salesforce.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.Salesforce;

public class PluginMigrationRunner : IHostedService
{
    private readonly SalesforceDbContextFactory _pluginDbContextFactory;
    public PluginMigrationRunner(SalesforceDbContextFactory pluginDbContextFactory)
    {
        _pluginDbContextFactory = pluginDbContextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var ctx = _pluginDbContextFactory.CreateContext();
        await using var dbContext = _pluginDbContextFactory.CreateContext();
        await ctx.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


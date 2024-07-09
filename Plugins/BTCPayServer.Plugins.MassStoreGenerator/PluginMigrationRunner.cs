using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.MassStoreGenerator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.Template;

public class PluginMigrationRunner : IHostedService
{
    private readonly MassStoreGeneratorDbContextFactory _pluginDbContextFactory;

    public PluginMigrationRunner(MassStoreGeneratorDbContextFactory pluginDbContextFactory)
    {
        _pluginDbContextFactory = pluginDbContextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var ctx = _pluginDbContextFactory.CreateContext();
        await ctx.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


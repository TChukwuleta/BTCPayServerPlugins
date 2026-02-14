using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.SquareSpace.Data;
using BTCPayServer.Plugins.SquareSpace.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.SquareSpace;

public class SquareSpacePlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("SquareSpaceNav", "header-nav"));
        services.AddScoped<SquarespaceService>();
        services.AddSingleton<SquareSpaceDbContextFactory>();
        services.AddDbContext<SquareSpaceDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<SquareSpaceDbContextFactory>();
            factory.ConfigureBuilder(o);
        });
        services.AddHostedService<PluginMigrationRunner>();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
    }
}
using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.LightSpeed;
using BTCPayServer.Plugins.LightSpeed.Data;
using BTCPayServer.Plugins.LightSpeed.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.SquareSpace;

public class LightSpeedPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("LightSpeedNav", "header-nav"));
        services.AddScoped<LightSpeedService>();
        services.AddSingleton<LightSpeedDbContextFactory>();
        services.AddDbContext<LightSpeedDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<LightSpeedDbContextFactory>();
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
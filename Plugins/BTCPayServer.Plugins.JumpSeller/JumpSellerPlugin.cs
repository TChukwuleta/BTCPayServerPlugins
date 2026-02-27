using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.JumpSeller.Data;
using BTCPayServer.Plugins.JumpSeller.Services;
using BTCPayServer.Plugins.LightSpeed;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.SquareSpace;

public class JumpSellerPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("JumpSellerNav", "header-nav"));
        services.AddScoped<JumpSellerService>();
        services.AddSingleton<JumpSellerDbContextFactory>();
        services.AddDbContext<JumpSellerDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<JumpSellerDbContextFactory>();
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
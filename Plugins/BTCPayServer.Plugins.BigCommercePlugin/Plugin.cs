using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.BigCommercePlugin;

public class Plugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=1.12.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("BigCommercePluginHeaderNav", "header-nav"));
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddSingleton<BigCommerceService>();
        services.AddHostedService<PluginMigrationRunner>();
        services.AddSingleton<BigCommerceDbContextFactory>();
    }
}

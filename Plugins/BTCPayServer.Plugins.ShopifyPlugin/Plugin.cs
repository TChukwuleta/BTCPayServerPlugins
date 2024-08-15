using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.ShopifyPlugin;
using BTCPayServer.Plugins.ShopifyPlugin.Services;
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
        services.AddSingleton<IUIExtension>(new UIExtension("ShopifyPluginHeaderNav", "header-nav"));
        services.AddSingleton<ShopifyHostedService>();
        services.AddHostedService<ShopifyHostedService>();
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddSingleton<ShopifyDbContextFactory>();
        services.AddDbContext<ShopifyDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<ShopifyDbContextFactory>();
            factory.ConfigureBuilder(o);
        });
        services.AddHostedService<PluginMigrationRunner>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAllOrigins", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
    }
}

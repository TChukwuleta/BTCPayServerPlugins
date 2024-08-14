using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.BigCommercePlugin.Services;
using BTCPayServer.Plugins.ShopifyPlugin;
using BTCPayServer.Plugins.ShopifyPlugin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

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
        services.AddSingleton<IHostedService, ShopifyService>();
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

    public override void Execute(IApplicationBuilder applicationBuilder, IServiceProvider applicationBuilderApplicationServices)
    {
        applicationBuilder.UseMiddleware<CorsMiddleware>();
        applicationBuilder.UseCors("AllowAllOrigins");

        base.Execute(applicationBuilder, applicationBuilderApplicationServices);
    }
}

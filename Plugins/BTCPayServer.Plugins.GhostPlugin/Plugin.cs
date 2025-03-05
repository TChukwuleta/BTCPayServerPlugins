using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.HostedServices.Webhooks;
using BTCPayServer.Plugins.GhostPlugin.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BTCPayServer.Plugins.GhostPlugin;

public class Plugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.0.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("GhostPluginHeaderNav", "header-nav"));
        services.AddSingleton<EmailService>();
        services.AddScoped<GhostPluginService>();
        services.AddSingleton<GhostHostedService>();
        services.AddSingleton<GhostDbContextFactory>();
        services.AddHostedService<GhostHostedService>();
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddHostedService(s => s.GetRequiredService<GhostPluginService>());
        services.AddDbContext<GhostDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<GhostDbContextFactory>();
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
        applicationBuilder.UseCors("AllowAllOrigins");
        base.Execute(applicationBuilder, applicationBuilderApplicationServices);
    }
}

using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.Salesforce.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BTCPayServer.Plugins.Salesforce;

public class Plugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.1.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("SalesforcePluginHeaderNav", "header-nav"));
        services.AddSingleton<SalesforceApiClient>();
        services.AddSingleton<SalesforceHostedService>();
        services.AddHostedService<SalesforceHostedService>();
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddSingleton<SalesforceDbContextFactory>();
        services.AddDbContext<SalesforceDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<SalesforceDbContextFactory>();
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

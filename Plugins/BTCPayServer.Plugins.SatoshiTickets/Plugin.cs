using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.SatoshiTickets.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BTCPayServer.Plugins.SatoshiTickets;

public class Plugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.0.7" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("SimpleTicketSalesPluginHeaderNav", "header-nav"));
        services.AddSingleton<EmailService>();
        services.AddSingleton<SimpleTicketSalesHostedService>();
        services.AddSingleton<SimpleTicketSalesDbContextFactory>();
        services.AddHostedService<SimpleTicketSalesHostedService>();
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddDbContext<SimpleTicketSalesDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<SimpleTicketSalesDbContextFactory>();
            factory.ConfigureBuilder(o);
        });
        services.AddHostedService<PluginMigrationRunner>();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

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
        applicationBuilder.UseSession();
        base.Execute(applicationBuilder, applicationBuilderApplicationServices);
    }
}

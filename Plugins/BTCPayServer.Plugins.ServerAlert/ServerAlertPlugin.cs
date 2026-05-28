using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.ServerAlert.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Plugins.ServerAlert;

public class ServerAlertPlugin : BaseBTCPayServerPlugin
{
    public const string Area = "ServerAlert";
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.9" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("ServerAlertAdminNav", "server-nav"));
        services.AddSingleton<IUIExtension>(new UIExtension("StoreAlertNav", "header-nav"));
        services.AddScoped<ServerAlertService>();
        services.AddSingleton<HealthMonitorService>();
        services.AddSingleton<IHostedService>(p => p.GetRequiredService<HealthMonitorService>());
        services.AddScheduledTask<HealthMonitorService>(TimeSpan.FromHours(1));
        services.AddSingleton<INotificationHandler, ServerAlertNotificationHandler>();
    }
}
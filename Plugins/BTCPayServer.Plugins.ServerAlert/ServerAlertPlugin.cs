using System;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.ServerAlert.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.ServerAlert;

public class ServerAlertPlugin : BaseBTCPayServerPlugin
{
    public const string Area = "ServerAlert";
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.7" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("ServerAlertAdminNav", "server-nav"));
        services.AddScoped<ServerAlertService>();

        services.AddSingleton<INotificationHandler, ServerAlertNotificationHandler>();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
    }
}
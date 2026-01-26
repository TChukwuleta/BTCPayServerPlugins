using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.StoreBridge.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.StoreBridge;

public class StoreBridgePlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.0" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("StoreBridgeNav", "header-nav"));
        services.AddScoped<StoreImportExportService>();
        services.AddScoped<StoreExportService>();
    }
}
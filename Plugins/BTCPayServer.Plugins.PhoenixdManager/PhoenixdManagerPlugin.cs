using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Plugins.PhoenixdManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.PhoenixdManager;

public class PhoenixdManagerPlugin : BaseBTCPayServerPlugin
{
    public override string Identifier => "BTCPayServer.Plugins.PhoenixdManager";
    public override string Name => "Phoenixd Manager";
    public override string Description =>
        "Full management UI for a phoenixd Lightning node: node info, balance, channels, send/receive over Lightning and on-chain, BOLT12 offers, LNURL, and live websocket events.";


    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.3.7" }
    };

    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<PhoenixdSettingsService>();
        services.AddHttpClient(PhoenixdClient.HttpClientName);
        services.AddSingleton<PhoenixdClient>();
        services.AddSingleton<IUIExtension>(new UIExtension("PhoenixdManagerNav", "header-nav"));
    }
}
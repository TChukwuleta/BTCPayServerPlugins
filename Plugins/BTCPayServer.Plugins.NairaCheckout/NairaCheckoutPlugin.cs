using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Hosting;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Mavapay.PaymentHandlers;
using BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;
using BTCPayServer.Plugins.NairaCheckout.Services;
using BTCPayServer.Plugins.Template.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.NairaCheckout;

public class NairaCheckoutPlugin : BaseBTCPayServerPlugin
{

    public const string PluginNavKey = nameof(NairaCheckoutPlugin) + "Nav";

    internal static PaymentMethodId NairaPmid = new("NAIRA");
    internal static string NairaDisplayName = "Naira";
    public const string SettingsName = "MavapayPluginSettings";

    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    [
        new() { Identifier = nameof(BTCPayServer), Condition = ">=2.1.0" }
    ];


    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("MavapayPayoutPluginHeaderNav", "header-nav"));
        services.AddMemoryCache();
        services.AddSingleton<GeneralCheckoutService>();
        services.AddSingleton<MavapayApiClientService>();
        services.AddHostedService<PluginMigrationRunner>();
        services.AddSingleton<NairaCheckoutHostedService>();
        services.AddHostedService<ApplicationPartsLogger>();
        services.AddHostedService<NairaCheckoutHostedService>();
        services.AddSingleton<NairaCheckoutDbContextFactory>();
        services.AddScoped<Safe>();
        services.AddDbContext<NairaCheckoutDbContext>((provider, o) =>
        {
            var factory = provider.GetRequiredService<NairaCheckoutDbContextFactory>();
            factory.ConfigureBuilder(o);
        });
        services.AddTransactionLinkProvider(NairaPmid, new NairaTransactionLinkProvider("naira"));
        services.AddSingleton(provider =>
            (IPaymentMethodHandler)ActivatorUtilities.CreateInstance(provider, typeof(NairaPaymentMethodHandler)));
        services.AddSingleton(provider =>
            (ICheckoutModelExtension)ActivatorUtilities.CreateInstance(provider, typeof(NairaCheckoutModelExtension)));

        services.AddDefaultPrettyName(NairaPmid, NairaDisplayName);

        services.AddSingleton<NairaStatusProvider>();
        services.AddUIExtension("store-wallets-nav", "NairaStoreNav"); 
        services.AddUIExtension("checkout-payment", "NairaLikeMethodCheckout");
        base.Execute(services);
    }
}
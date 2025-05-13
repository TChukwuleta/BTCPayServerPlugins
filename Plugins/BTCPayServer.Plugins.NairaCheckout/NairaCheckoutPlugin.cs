using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Hosting;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Mavapay.PaymentHandlers;
using BTCPayServer.Plugins.Template.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Template;

public class NairaCheckoutPlugin : BaseBTCPayServerPlugin
{

    public const string PluginNavKey = nameof(NairaCheckoutPlugin) + "Nav";

    internal static PaymentMethodId NairaPmid = new("NAIRA");
    internal static string NairaDisplayName = "Naira";

    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    [
        new() { Identifier = nameof(BTCPayServer), Condition = ">=2.1.0" }
    ];


    public override void Execute(IServiceCollection services)
    {
        services.AddTransactionLinkProvider(NairaPmid, new NairaTransactionLinkProvider("naira"));

        services.AddSingleton(provider =>
            (IPaymentMethodHandler)ActivatorUtilities.CreateInstance(provider, typeof(CashPaymentMethodHandler)));
        services.AddSingleton(provider =>
            (ICheckoutModelExtension)ActivatorUtilities.CreateInstance(provider, typeof(CashCheckoutModelExtension)));

        services.AddDefaultPrettyName(CashPmid, CashDisplayName);

        //
        services.AddSingleton<CashStatusProvider>();

        //
        services.AddUIExtension("store-wallets-nav", "CashStoreNav");
        services.AddUIExtension("checkout-payment", "CashLikeMethodCheckout");

        base.Execute(services);
    }


    public override void Execute(IServiceCollection services)
    {
        services.AddTransactionLinkProvider(NairaPmid, new CashTransactionLinkProvider("cash"));

        services.AddSingleton(provider =>
            (IPaymentMethodHandler)ActivatorUtilities.CreateInstance(provider, typeof(CashPaymentMethodHandler)));
        services.AddSingleton(provider =>
            (ICheckoutModelExtension)ActivatorUtilities.CreateInstance(provider, typeof(CashCheckoutModelExtension))); 

        services.AddDefaultPrettyName(CashPmid, CashDisplayName);



    public override void Execute(IServiceCollection services)
    {
        services.AddSingleton<IUIExtension>(new UIExtension("MassStoreGeneratorPluginHeaderNav", "header-nav"));
        services.AddHostedService<ApplicationPartsLogger>();
    }


    //
    services.AddSingleton<CashStatusProvider>();

        //
        services.AddUIExtension("store-wallets-nav", "CashStoreNav");
        services.AddUIExtension("checkout-payment", "CashLikeMethodCheckout");

        base.NairaCheckoutPlugin(services);
    }

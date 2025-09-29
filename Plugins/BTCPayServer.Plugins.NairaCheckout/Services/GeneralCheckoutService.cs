using System;
using System.Threading.Tasks;
using BTCPayServer.Configuration;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Wallets;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class GeneralCheckoutService
{
    private readonly StoreRepository _storeRepository;
    private readonly BTCPayWalletProvider _walletProvider;
    private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
    private readonly IOptions<LightningNetworkOptions> _lightningNetworkOptions;
    private readonly LightningClientFactoryService _lightningClientFactoryService;
    private readonly PaymentMethodHandlerDictionary _paymentMethodHandlerDictionary;

    public GeneralCheckoutService(StoreRepository storeRepository,
        BTCPayWalletProvider walletProvider,
        BTCPayNetworkProvider btcPayNetworkProvider,
        IOptions<LightningNetworkOptions> lightningNetworkOptions,
        LightningClientFactoryService lightningClientFactoryService,
        PaymentMethodHandlerDictionary paymentMethodHandlerDictionary)
    {
        _walletProvider = walletProvider;
        _storeRepository = storeRepository;
        _btcPayNetworkProvider = btcPayNetworkProvider;
        _lightningNetworkOptions = lightningNetworkOptions;
        _lightningClientFactoryService = lightningClientFactoryService;
        _paymentMethodHandlerDictionary = paymentMethodHandlerDictionary;
    }


    public async Task<LightMoney> GetLightningNodeBalance(string storeId)
    {
        try
        {
            var lnClient = new Lazy<Task<ILightningClient>>(async () => await ConstructLightningClient(storeId));
            var client = await lnClient.Value;
            if (client is null) return LightMoney.Zero;

            var balance = await client.GetBalance();
            return balance.OffchainBalance.Local;

        }
        catch (Exception){ return LightMoney.Zero; }
    }

    public async Task<LightMoney> GetOnChainBalance(string storeId)
    {
        try
        {
            var onchainBalance = new Lazy<Task<LightMoney>>(async () =>
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store is null) return null;
                var settings = store.GetDerivationSchemeSettings(_paymentMethodHandlerDictionary, "BTC", true);
                if (settings is null) return null;
                var wallet = _walletProvider.GetWallet("BTC");
                var res = await wallet.GetBalance(settings.AccountDerivation);
                return new LightMoney(Money.Coins(res.Available.GetValue(wallet.Network)));
            });
            var walletBalance = await onchainBalance.Value;
            return walletBalance ?? LightMoney.Zero;
        }
        catch (Exception) { return LightMoney.Zero; }
    }


    private async Task<ILightningClient> ConstructLightningClient(string storeId)
    {
        var store = await _storeRepository.FindStore(storeId);

        var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>("BTC");
        var id = PaymentTypes.LN.GetPaymentMethodId("BTC");
        var existing =
            store.GetPaymentMethodConfig<LightningPaymentMethodConfig>(id,
                _paymentMethodHandlerDictionary);
        if (existing?.GetExternalLightningUrl() is { } connectionString)
        {
            return _lightningClientFactoryService.Create(connectionString,
                network);
        }
        else if (existing?.IsInternalNode is true &&
                 _lightningNetworkOptions.Value.InternalLightningByCryptoCode
                     .TryGetValue(network.CryptoCode,
                         out var internalLightningNode))
        {
            return internalLightningNode;
        }
        return null;

    }
}

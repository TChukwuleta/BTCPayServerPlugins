using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Template;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;

namespace BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;

public class NairaStatusProvider(
    StoreRepository storeRepository,
    PaymentMethodHandlerDictionary handlers)
{
    public async Task<bool> NairaEnabled(string storeId)
    {
        try
        {
            var storeData = await storeRepository.FindStore(storeId);
            var currentPaymentMethodConfig =
                storeData.GetPaymentMethodConfig<CashPaymentMethodConfig>(NairaCheckoutPlugin.NairaPmid, handlers);
            if (currentPaymentMethodConfig == null)
                return false;

            var excludeFilters = storeData.GetStoreBlob().GetExcludedPaymentMethods();
            var enabled = !excludeFilters.Match(NairaCheckoutPlugin.NairaPmid);

            return enabled;
        }
        catch
        {
            return false;
        }
    }
}

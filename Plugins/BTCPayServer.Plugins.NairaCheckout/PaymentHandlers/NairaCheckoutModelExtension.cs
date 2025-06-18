using System;
using BTCPayServer.Payments;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;

public class NairaCheckoutModelExtension : ICheckoutModelExtension
{
    public const string CheckoutBodyComponentName = "NAIRACheckout";

    public PaymentMethodId PaymentMethodId => NairaCheckoutPlugin.NairaPmid;
    public string Image => "";
    public string Badge => "";

    public void ModifyCheckoutModel(CheckoutModelContext context)
    {
        if (context is not { Handler: NairaPaymentMethodHandler handler })
            return;

        context.Model.CheckoutBodyComponentName = CheckoutBodyComponentName;

        context.Model.InvoiceBitcoinUrlQR = null;
        context.Model.ExpirationSeconds = int.MaxValue;
        context.Model.Activated = true;

        context.Model.InvoiceBitcoinUrl = $"/stores/{context.Model.StoreId}/naira/MarkAsPaid?" +
                                          $"invoiceId={context.Model.InvoiceId}&" +
                                          $"returnUrl=/i/{context.Model.InvoiceId}";
        context.Model.ShowPayInWalletButton = true;
        Console.WriteLine(JsonConvert.SerializeObject(context.Model));
    }
}
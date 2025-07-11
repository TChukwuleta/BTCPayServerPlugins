﻿using System;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Services.Rates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.NairaCheckout.PaymentHandlers;

public class NairaPaymentMethodHandler(CurrencyNameTable currencyNameTable) : IPaymentMethodHandler
{
    public PaymentMethodId PaymentMethodId => NairaCheckoutPlugin.NairaPmid;

    public Task ConfigurePrompt(PaymentMethodContext context)
    {
        return Task.CompletedTask;
    }

    public Task BeforeFetchingRates(PaymentMethodContext context)
    {
        var currency = currencyNameTable.GetCurrencyData(context.InvoiceEntity.Currency, false);

        context.Prompt.Currency = currency.Code;
        context.Prompt.Divisibility = currency.Divisibility;
        context.Prompt.RateDivisibility = currency.Divisibility;
        return Task.CompletedTask;
    }

    public JsonSerializer Serializer { get; } = BlobSerializer.CreateSerializer().Serializer;

    public object ParsePaymentPromptDetails(JToken details)
    {
        return details.ToObject<CashPaymentMethodDetails>(Serializer);
    }

    public object ParsePaymentMethodConfig(JToken config)
    {
        return config.ToObject<CashPaymentMethodConfig>(Serializer) ??
               throw new FormatException($"Invalid {nameof(NairaPaymentMethodHandler)}");
    }

    public object ParsePaymentDetails(JToken details)
    {
        return details.ToObject<CashPaymentData>(Serializer) ??
               throw new FormatException($"Invalid {nameof(NairaPaymentMethodHandler)}");
    }
}

public class CashPaymentData
{
}

public class CashPaymentMethodConfig
{
}

public class CashPaymentMethodDetails
{
}

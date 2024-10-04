using BTCPayServer.Data;
using BTCPayServer.Plugins.Shopify.Models;
using BTCPayServer.Plugins.ShopifyPlugin.Data;
using BTCPayServer.Plugins.ShopifyPlugin.Services;
using BTCPayServer.Plugins.ShopifyPlugin.ViewModels.Models;
using NBitcoin;
using NBXplorer;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace BTCPayServer.Plugins.ShopifyPlugin.Helper;

public static class ShopifyExtensions
{
    public const string StoreBlobKey = "shopify";
    public static ShopifyApiClientCredentials CreateShopifyApiCredentials(this ShopifySetting shopify)
    {
        return new ShopifyApiClientCredentials
        {
            ShopName = shopify.ShopName,
            ApiKey = shopify.ApiKey,
            ApiPassword = shopify.Password
        };
    }

    public static ShopifySettings GetShopifySettings(this StoreBlob storeBlob)
    {
        if (storeBlob.AdditionalData.TryGetValue(StoreBlobKey, out var rawS))
        {
            if (rawS is JObject rawObj)
            {
                return new Serializer(null).ToObject<ShopifySettings>(rawObj);
            }
            else if (rawS.Type == JTokenType.String)
            {
                return new Serializer(null).ToObject<ShopifySettings>(rawS.Value<string>());
            }
        }
        return null;
    }

    public static void SetShopifySettings(this StoreBlob storeBlob, ShopifySettings settings)
    {
        if (settings is null)
        {
            storeBlob.AdditionalData.Remove(StoreBlobKey);
        }
        else
        {
            storeBlob.AdditionalData.AddOrReplace(StoreBlobKey, new Serializer(null).ToString(settings));
        }
    }

    public static ShopifyOrderResponseViewModel GetShopifyOrderResponse(this ShopifyOrderVm order)
    {
        return new ShopifyOrderResponseViewModel
        {
            Id = order.Id,
            CartToken = order.CartToken,
            CheckoutId = order.CheckoutId,
            CheckoutToken = order.CheckoutToken,
            ConfirmationNumber = order.ConfirmationNumber,
            Confirmed = order.Confirmed,
            Currency = order.Currency,
            CurrentSubtotalPrice = order.CurrentSubtotalPrice,
            CurrentTotalPrice = order.CurrentTotalPrice,
            FinancialStatus = order.FinancialStatus,
            Number = order.Number,
            OrderNumber = order.OrderNumber
        };
    }

    public static List<ShopifyOrderResponseViewModel> GetShopifyOrderResponse(this List<ShopifyOrderVm> orders)
    {
        return orders.Select(order => new ShopifyOrderResponseViewModel
        {
            Id = order.Id,
            CartToken = order.CartToken,
            CheckoutId = order.CheckoutId,
            CheckoutToken = order.CheckoutToken,
            ConfirmationNumber = order.ConfirmationNumber,
            Confirmed = order.Confirmed,
            Currency = order.Currency,
            CurrentSubtotalPrice = order.CurrentSubtotalPrice,
            CurrentTotalPrice = order.CurrentTotalPrice,
            FinancialStatus = order.FinancialStatus,
            Number = order.Number,
            OrderNumber = order.OrderNumber
        }).ToList();
    }
}
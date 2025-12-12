using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.NairaCheckout.Data;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.NairaCheckout.Services;

public class NairaCheckoutHostedService : EventHostedServiceBase
{
    private readonly StoreRepository _storeRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly GeneralCheckoutService _generalCheckoutService;
    private readonly NairaCheckoutDbContextFactory _dbContextFactory;
    private readonly MavapayApiClientService _mavapayApiClientService;

    public NairaCheckoutHostedService(EventAggregator eventAggregator,
        StoreRepository storeRepository,
        InvoiceRepository invoiceRepository,
        GeneralCheckoutService generalCheckoutService,
        NairaCheckoutDbContextFactory dbContextFactory,
        MavapayApiClientService mavapayApiClientService,
        Logs logs) : base(eventAggregator, logs)
    {
        _storeRepository = storeRepository;
        _dbContextFactory = dbContextFactory;
        _invoiceRepository = invoiceRepository;
        _mavapayApiClientService = mavapayApiClientService;
        _generalCheckoutService = generalCheckoutService;
    }

    protected override void SubscribeToEvents()
    {
        Subscribe<InvoiceEvent>();
        base.SubscribeToEvents();
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        switch (evt)
        {
            case InvoiceEvent invoiceEvent when new[]
            {
            InvoiceEvent.MarkedCompleted,
            InvoiceEvent.MarkedInvalid,
            InvoiceEvent.Expired,
            InvoiceEvent.Confirmed,
            InvoiceEvent.Completed
        }.Contains(invoiceEvent.Name):
                {
                    var invoice = invoiceEvent.Invoice;
                    bool? success = invoice.Status switch
                    {
                        InvoiceStatus.Settled => true,
                        InvoiceStatus.Invalid or
                        InvoiceStatus.Expired => false,
                        _ => (bool?)null
                    };
                    if (success.HasValue)
                    {
                        await RegisterTransactionOrder(invoice, success.Value);
                    }
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }


    private async Task RegisterTransactionOrder(InvoiceEntity invoice, bool success)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var order = ctx.NairaCheckoutOrders.FirstOrDefault(c => c.InvoiceId == invoice.Id && c.StoreId == invoice.StoreId);
        if (order == null) return;

        var result = new InvoiceLogs();
        result.Write($"Invoice status: {invoice.Status.ToString().ToLower()}", InvoiceEventData.EventSeverity.Info);
        result.Write($"Writing Naira checkout transaction payment", InvoiceEventData.EventSeverity.Info);
        try
        {
            order.ThirdPartyStatus = success ? invoice.Status.ToString() : invoice.ExceptionStatus.ToString();
            order.ThirdPartyMarkedPaid = success;
            order.UpdatedAt = DateTime.UtcNow;
            ctx.NairaCheckoutOrders.Update(order);
            await ctx.SaveChangesAsync();
            var settings = await _storeRepository.GetSettingAsync<MavapayCheckoutSettings>(invoice.StoreId, NairaCheckoutPlugin.SettingsName) ?? new MavapayCheckoutSettings();
            if (settings.EnableSplitPayment && invoice.Currency.ToLower().Contains("ngn"))
            {
                var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == invoice.StoreId);
                var store = await _storeRepository.GetStoreByInvoiceId(invoice.Id);
                var lightningBalance = await GetLightningBalance(invoice.StoreId);

                decimal amount = invoice.NetSettled * (settings.SplitPercentage / 100m);
                var parsed = Enum.TryParse<SupportedCurrency>(settings.Currency, out var currency);
                switch (parsed ? currency : default)
                {
                    case SupportedCurrency.NGN:
                        var ngnPayout = await _mavapayApiClientService.MavapayNairaPayout(new PayoutNGNViewModel
                        {
                            AccountName = settings.NGNAccountName,
                            BankCode = settings.NGNBankCode,
                            BankName = settings.NGNBankName,
                            AccountNumber = settings.NGNAccountNumber,
                            Amount = amount
                        }, mavapaySetting.ApiKey);

                        if (lightningBalance > ngnPayout.totalAmountInSourceCurrency)
                        {
                            await _mavapayApiClientService.ClaimPayout(ctx, ngnPayout, store, SupportedCurrency.NGN.ToString(), settings.NGNAccountNumber);
                        }
                        break;

                    case SupportedCurrency.KES:
                        var kesPayout = await _mavapayApiClientService.MavapayKenyanShillingPayout(new PayoutKESViewModel
                        {
                            Method = settings.KESMethod,
                            AccountNumber = settings.KESAccountNumber,
                            AccountName = settings.KESAccountName,
                            Identifier = settings.KESIdentifier,
                            Amount = amount
                        }, mavapaySetting.ApiKey);

                        if (lightningBalance > kesPayout.totalAmountInSourceCurrency)
                        {
                            await _mavapayApiClientService.ClaimPayout(ctx, kesPayout, store, SupportedCurrency.KES.ToString(), settings.KESAccountNumber);
                        }
                        break;

                    case SupportedCurrency.ZAR:
                        var zarPayout = await _mavapayApiClientService.MavapayRandsPayout(new PayoutZARViewModel
                        {
                            Bank = settings.ZARBank,
                            AccountName = settings.ZARAccountName,
                            AccountNumber = settings.ZARAccountNumber,
                            Amount = amount
                        }, mavapaySetting.ApiKey);

                        if (lightningBalance > zarPayout.totalAmountInSourceCurrency)
                        {
                            await _mavapayApiClientService.ClaimPayout(ctx, zarPayout, store, SupportedCurrency.ZAR.ToString(), settings.ZARAccountNumber);
                        }
                        break;

                    default:
                        break;
                }

            }
            result.Write($"Successfully recored naira checkout.", InvoiceEventData.EventSeverity.Info);
        }
        catch (Exception ex)
        {
            Logs.PayServer.LogError(ex, $"Naira plugin error. {ex.Message} Triggered by invoiceId: {invoice.Id}");
        }
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }

    private async Task<long> GetLightningBalance(string storeId)
    {
        var balance = await _generalCheckoutService.GetLightningNodeBalance(storeId);
        return balance.MilliSatoshi / 1000;
    }
}

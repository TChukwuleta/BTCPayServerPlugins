using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Lightning.LndHub;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.NairaCheckout.Data;
using BTCPayServer.Plugins.NairaCheckout.ViewModels;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
                        await HandleSplitPayment(invoice);
                    }
                    break;
                }
        }
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task RegisterTransactionOrder(InvoiceEntity invoice, bool success)
    {
        var result = new InvoiceLogs();
        await using var ctx = _dbContextFactory.CreateContext();
        var order = ctx.NairaCheckoutOrders.FirstOrDefault(c => c.InvoiceId == invoice.Id && c.StoreId == invoice.StoreId);
        if (order == null) return;

        result.Write($"Writing Naira checkout transaction payment", InvoiceEventData.EventSeverity.Info);
        try
        {
            order.ThirdPartyStatus = success ? invoice.Status.ToString() : invoice.ExceptionStatus.ToString();
            order.ThirdPartyMarkedPaid = success;
            order.UpdatedAt = DateTime.UtcNow;
            ctx.NairaCheckoutOrders.Update(order);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logs.PayServer.LogError(ex, $"Naira plugin error. {ex.Message} Triggered by invoiceId: {invoice.Id}");
        }
        await _invoiceRepository.AddInvoiceLogs(invoice.Id, result);
    }

    bool IsInvoiceCurrencyPayout(string invoiceCurrency, SupportedCurrency payoutCurrency)
    {
        return invoiceCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase) || invoiceCurrency.Equals(payoutCurrency.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    static readonly AsyncDuplicateLock PayoutLocks = new AsyncDuplicateLock();
    private async Task HandleSplitPayment(InvoiceEntity invoice)
    {
        if (invoice.Status != InvoiceStatus.Settled) return;
        await using var ctx = _dbContextFactory.CreateContext();
        var settledPayout = ctx.PayoutTransactions.FirstOrDefault(p => p.ExternalReference.EndsWith($":{invoice.Id}"));
        if (settledPayout != null) return;
        var result = new InvoiceLogs();
        try
        {
            using var l = await PayoutLocks.LockAsync(invoice.Id, CancellationToken.None);

            settledPayout = ctx.PayoutTransactions.FirstOrDefault(p => p.ExternalReference.EndsWith($":{invoice.Id}"));
            if (settledPayout != null) return;

            var settings = await _storeRepository.GetSettingAsync<MavapayCheckoutSettings>(invoice.StoreId, NairaCheckoutPlugin.SettingsName) ?? new MavapayCheckoutSettings();
            var mavapaySetting = ctx.MavapaySettings.FirstOrDefault(c => c.StoreId == invoice.StoreId);
            if (string.IsNullOrEmpty(mavapaySetting?.ApiKey) || !settings.EnableSplitPayment)
                return;

            if (!Enum.TryParse<SupportedCurrency>(settings.Currency, true, out var payoutCurrency))
                return;

            if (!IsInvoiceCurrencyPayout(invoice.Currency, payoutCurrency))
                return;

            var store = await _storeRepository.GetStoreByInvoiceId(invoice.Id);
            var lightningBalance = await GetLightningBalance(invoice.StoreId);
            decimal splitAmount = invoice.NetSettled * (settings.SplitPercentage / 100m);

            if (invoice.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                var bidRate = await _mavapayApiClientService.GetUSDToBidRate(payoutCurrency);
                if (bidRate <= 0) return;

                splitAmount = splitAmount * bidRate;
            }
            if (splitAmount <= 0) return;

            splitAmount = Math.Round(splitAmount, 2);

            CreatePayoutResponseModel payoutResponse = null;
            string accountIdentifier = null;
            switch (payoutCurrency)
            {
                case SupportedCurrency.NGN:
                    var nameEnquiry = await _mavapayApiClientService.NGNNameEnquiry(settings.NGNBankCode, settings.NGNAccountNumber, mavapaySetting.ApiKey);
                    if (nameEnquiry == null || string.IsNullOrEmpty(nameEnquiry.accountName)) return;

                    accountIdentifier = settings.NGNAccountNumber;
                    payoutResponse = await _mavapayApiClientService.MavapayNairaPayout(new PayoutNGNViewModel
                    {
                        AccountName = nameEnquiry.accountName,
                        BankCode = settings.NGNBankCode,
                        BankName = settings.NGNBankCode,
                        AccountNumber = settings.NGNAccountNumber,
                        Amount = splitAmount
                    }, mavapaySetting.ApiKey);
                    break;

                case SupportedCurrency.KES:
                    accountIdentifier = settings.KESIdentifier;
                    payoutResponse = await _mavapayApiClientService.MavapayKenyanShillingPayout(new PayoutKESViewModel
                    {
                        Method = settings.KESMethod,
                        AccountNumber = settings.KESAccountNumber,
                        AccountName = settings.KESAccountName,
                        Identifier = settings.KESIdentifier,
                        Amount = splitAmount
                    }, mavapaySetting.ApiKey);
                    break;

                case SupportedCurrency.ZAR:
                    accountIdentifier = settings.ZARAccountNumber;
                    payoutResponse = await _mavapayApiClientService.MavapayRandsPayout(new PayoutZARViewModel
                    {
                        Bank = settings.ZARBank,
                        AccountName = settings.ZARAccountName,
                        AccountNumber = settings.ZARAccountNumber,
                        Amount = splitAmount
                    }, mavapaySetting.ApiKey);
                    break;

                default:
                    return;
            }
            if (!string.IsNullOrEmpty(payoutResponse.ErrorMessage)) return;

            if (lightningBalance <= payoutResponse.totalAmountInSourceCurrency) return;

            await _mavapayApiClientService.ClaimPayout(ctx, payoutResponse, store, payoutCurrency.ToString(), accountIdentifier, invoice.Id);
            result.Write($"Successfully recorded naira checkout.", InvoiceEventData.EventSeverity.Info);
        }
        catch (Exception ex)
        {
            result.Write($"An error occured.. {ex.Message}", InvoiceEventData.EventSeverity.Error);
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

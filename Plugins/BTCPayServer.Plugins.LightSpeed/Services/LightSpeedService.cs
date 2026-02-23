using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Plugins.LightSpeed.Data;

namespace BTCPayServer.Plugins.LightSpeed.Services;

public class LightSpeedService
{
    private readonly LightSpeedDbContextFactory _dbContextFactory;
    public LightSpeedService(LightSpeedDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LightspeedSettings?> GetSettings(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        return ctx.LightspeedSettings.FirstOrDefault(c => c.StoreId == storeId);
    }

    public async Task SaveSettingsAsync(LightspeedSettings settings)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var existingSetting = ctx.LightspeedSettings.FirstOrDefault(c => c.StoreId == settings.StoreId);
        if (existingSetting is null)
            ctx.LightspeedSettings.Add(settings);
        else
        {
            existingSetting.LightspeedDomainPrefix = settings.LightspeedDomainPrefix;
            existingSetting.LightspeedPersonalAccessToken = settings.LightspeedPersonalAccessToken;
            existingSetting.Currency = settings.Currency;
            ctx.LightspeedSettings.Update(existingSetting);
        }
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteSettingsAsync(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var settings = ctx.LightspeedSettings.FirstOrDefault(c => c.StoreId == storeId);
        if (settings is not null)
        {
            ctx.LightspeedSettings.Remove(settings);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task AddLightSpeedPayment(LightSpeedPayment payment)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        ctx.LightSpeedPayments.Add(payment);
        await ctx.SaveChangesAsync();
    }

    public async Task<LightSpeedPayment?> GetPayment(string invoiceId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        return ctx.LightSpeedPayments.FirstOrDefault(c => c.InvoiceId == invoiceId);
    }

    public async Task UpdatePaymentStatus(string invoiceId, InvoiceStatus status)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        var payment = ctx.LightSpeedPayments.FirstOrDefault(c => c.InvoiceId == invoiceId);
        if (payment is not null)
        {
            payment.InvoiceStatus = status.ToString();
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<LightSpeedPayment>> GetStorePayment(string storeId)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        return ctx.LightSpeedPayments.Where(p => p.StoreId == storeId).ToList();
    }
}

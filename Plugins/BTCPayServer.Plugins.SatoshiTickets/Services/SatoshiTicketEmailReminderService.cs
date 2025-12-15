using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using BTCPayServer.Services.Stores;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public class SatoshiTicketEmailReminderService(
    StoreRepository storeRepository,
    EmailService emailService, 
    IScopeProvider ScopeProvider,
    EventAggregator eventAggregator,
    SimpleTicketSalesDbContextFactory _dbContextFactory,
    Logs logs)
    : EventHostedServiceBase(eventAggregator, logs), IPeriodicTask
{
    public async Task Do(CancellationToken cancellationToken)
    {
        try
        {
            var storeId = ScopeProvider.GetCurrentStoreId();
            Console.WriteLine(storeId);
            var settings = await storeRepository.GetSettingAsync<SatoshiTicketSettings>(storeId, Plugin.SettingsName) ?? new SatoshiTicketSettings();
            if (!settings.EnableEventAutoReminder) return;

            PushEvent(new SatoshiTicketEmailReminderProcessEvent { StoreId = storeId });
        }
        catch (Exception){ }
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        if (evt is SatoshiTicketEmailReminderProcessEvent sequentialExecute) await HandleEmailReminders(sequentialExecute.StoreId);

        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task HandleEmailReminders(string storeId)
    {
        var shouldUpdateDb = false;


        var settings = await storeRepository.GetSettingAsync<SatoshiTicketSettings>(storeId, Plugin.SettingsName) ?? new SatoshiTicketSettings();
        if (!settings.EnableEventAutoReminder) return;

        await using var ctx = _dbContextFactory.CreateContext();
    }

    public class SatoshiTicketEmailReminderProcessEvent
    {
        public string StoreId { get; set; }
    }
}

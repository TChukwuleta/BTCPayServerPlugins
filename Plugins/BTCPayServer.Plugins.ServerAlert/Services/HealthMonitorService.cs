using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Lightning;
using BTCPayServer.Logging;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Plugins.ServerAlert.Data;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.ServerAlert.Services;


public class HealthMonitorService(
    EventAggregator eventAggregator,
    IServiceScopeFactory scopeFactory,
    BTCPayNetworkProvider networkProvider,
    ExplorerClientProvider explorerClientProvider,
    PaymentMethodHandlerDictionary paymentHandlers,
    LightningClientFactoryService lightningClientFactory,
    ILogger<HealthMonitorService> logger,
    Logs logs) : EventHostedServiceBase(eventAggregator, logs), IPeriodicTask
{
    private readonly Dictionary<string, bool> _alertFired = new();

    protected override void SubscribeToEvents()
    {
        Subscribe<StoreCheckEvent>();
        Subscribe<ServerCheckEvent>();
    }

    public async Task Do(CancellationToken cancellationToken)
    {
        logger.LogInformation("HealthMonitorService: Do() triggered");
        await WithScope(async (alertService, storeRepo, settingsRepo, pullPaymentService) =>
        {
            var serverSettings = await settingsRepo.GetSettingAsync<ServerMonitorSettings>() ?? new();
            logger.LogInformation("HealthMonitorService: Server monitor enabled = {Enabled}", serverSettings.Enabled);
            if (serverSettings.Enabled)
                PushEvent(new ServerCheckEvent { Settings = serverSettings });

            var stores = await storeRepo.GetStores();
            foreach (var store in stores)
            {
                var key = $"StoreMonitor_{store.Id}";
                var storeSettings = await settingsRepo.GetSettingAsync<StoreMonitorSettings>(key) ?? new();
                if (!storeSettings.Enabled) continue;
                PushEvent(new StoreCheckEvent { Store = store, Settings = storeSettings });
            }
        });
    }

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        await WithScope(async (alertService, storeRepo, settingsRepo, pullPaymentService) =>
        {
            switch (evt)
            {
                case ServerCheckEvent s:
                    if (s.Settings.MonitorBitcoinNode)
                        await RunServerCheck("server:bitcoin", "Bitcoin Node / NBXplorer", CheckBitcoinNode, s.Settings, alertService, settingsRepo);
                    break;

                case StoreCheckEvent e:
                    await RunStoreChecks(e.Store, e.Settings, settingsRepo, storeRepo, alertService, pullPaymentService);
                    break;
            }
        });
        await base.ProcessEvent(evt, cancellationToken);
    }

    private async Task RunServerCheck(string key, string name, Func<Task<(MonitorStatus, string)>> fn, ServerMonitorSettings settings,
        ServerAlertService serverAlertService, SettingsRepository settingsRepository)
    {
        var (status, message) = await fn();

        settings.RecentAlerts ??= new();
        settings.RecentAlerts.Insert(0, new MonitorAlertLog
        {
            CheckName = name,
            Message = message,
            Status = status,
            FiredAt = DateTimeOffset.UtcNow
        });
        if (settings.RecentAlerts.Count > 50)
            settings.RecentAlerts = settings.RecentAlerts.Take(50).ToList();

        await settingsRepository.UpdateSetting(settings);
        await HandleResult(
            key: key,
            title: $"[Server Alert] {name} issue detected",
            recoveryTitle: $"[Server Recovered] {name} is back to normal",
            message: message,
            status: status,
            emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.AdminsOnly,
            customEmails: null,
            settingsRepository: settingsRepository,
            serverAlertService: serverAlertService);
    }

    private async Task<(MonitorStatus, string)> CheckBitcoinNode()
    {
        try
        {
            logger.LogInformation("HealthMonitorService: Running Bitcoin node check");
            var client = explorerClientProvider.GetExplorerClient("BTC");
            if (client is null)
                return (MonitorStatus.Critical, "Cannot reach Bitcoin node — explorer client unavailable.");

            var status = await client.GetStatusAsync();
            logger.LogInformation("HealthMonitorService: Bitcoin node status — synced={Synced}, height={Height}", status.IsFullySynched, status.ChainHeight);
            if (status is null)
                return (MonitorStatus.Critical, "Bitcoin node is not responding.");

            if (!status.IsFullySynched)
                return (MonitorStatus.Warning, $"Bitcoin node is syncing. Chain height: {status.ChainHeight}.");

            return (MonitorStatus.Healthy, "Bitcoin node is online and synced.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HealthMonitorService: Bitcoin node check threw exception");
            return (MonitorStatus.Critical, $"Bitcoin node check failed: {ex.Message}");
        }
    }

    /*private Task<(MonitorStatus, string)> CheckBitcoinNode()
    {
        return Task.FromResult((MonitorStatus.Critical, "Test alert — simulated failure"));
    }*/

    private async Task RunStoreChecks(BTCPayServer.Data.StoreData store, StoreMonitorSettings settings, 
        SettingsRepository settingsRepository, StoreRepository storeRepository, ServerAlertService serverAlertService, PullPaymentHostedService pullPaymentService)
    {
        if (settings.AlertOnUnprocessedPayout)
        {
            var key = $"store:{store.Id}:payout";
            var threshold = DateTimeOffset.UtcNow.AddHours(-settings.UnprocessedPayoutThresholdHours);

            var payouts = await pullPaymentService.GetPayouts(new PullPaymentHostedService.PayoutQuery
            {
                Stores = new[] { store.Id },
                States = new[] { PayoutState.AwaitingPayment },
            });
            var stalePayouts = payouts.Where(p => p.Date < threshold).ToList();
            var isStale = stalePayouts.Any();

            await HandleResult(
                key: key,
                title: $"Store '{store.StoreName}': {stalePayouts.Count} payout(s) awaiting processing",
                recoveryTitle: $"Store '{store.StoreName}': Payouts are up to date",
                message: isStale
                    ? $"{stalePayouts.Count} approved payout(s) have not been sent for more than " +
                      $"{settings.UnprocessedPayoutThresholdHours} hours. Log in to process or cancel them."
                    : string.Empty,
                status: isStale ? MonitorStatus.Warning : MonitorStatus.Healthy,
                emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.CustomEmails,
                customEmails: await GetOwnerEmails(store.Id, storeRepository),
                serverAlertService: serverAlertService,
                settingsRepository: settingsRepository);
        }

        if (settings.AlertOnLightningNodeOffline || settings.AlertOnLowLightningInbound)
        {
            try
            {

                var paymentMethodId = PaymentMethodId.Parse("BTC-LN");
                var config = store.GetPaymentMethodConfig<LightningPaymentMethodConfig>(paymentMethodId, paymentHandlers, onlyEnabled: true);
                if (config is null || string.IsNullOrEmpty(config.ConnectionString)) return;

                var network = networkProvider.GetNetwork<BTCPayNetwork>("BTC");
                var client = lightningClientFactory.Create(config.ConnectionString, network);
                if (client is null) return;

                LightningNodeInformation info = null;
                var offlineKey = $"store:{store.Id}:lightning:offline";
                var inboundKey = $"store:{store.Id}:lightning:inbound";

                try { info = await client.GetInfo(); }
                catch (Exception ex)
                {
                    if (settings.AlertOnLightningNodeOffline)
                    {
                        await HandleResult(
                            key: offlineKey,
                            title: $"Store '{store.StoreName}': Lightning node unreachable",
                            recoveryTitle: $"Store '{store.StoreName}': Lightning node recovered",
                            message: $"Could not connect to Lightning node: {ex.Message}",
                            status: MonitorStatus.Critical,
                            emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.CustomEmails,
                            customEmails: await GetOwnerEmails(store.Id, storeRepository),
                            serverAlertService: serverAlertService,
                            settingsRepository: settingsRepository);
                    }
                    return;
                }

                if (settings.AlertOnLightningNodeOffline)
                {
                    var isOffline = info is null;

                    await HandleResult(
                        key: offlineKey,
                        title: isOffline ? $"Store '{store.StoreName}': Lightning node not responding" : $"Store '{store.StoreName}': Lightning node recovered",
                        recoveryTitle: $"Store '{store.StoreName}': Lightning node recovered",
                        message: isOffline ? "The Lightning node returned no information. It may be starting up or offline." : string.Empty,
                        status: isOffline ? MonitorStatus.Critical : MonitorStatus.Healthy,
                        emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.CustomEmails,
                        customEmails: await GetOwnerEmails(store.Id, storeRepository),
                        serverAlertService: serverAlertService,
                        settingsRepository: settingsRepository);

                    if (isOffline) return;
                }
                if (settings.AlertOnLowLightningInbound && info is not null)
                {
                    var channels = await client.ListChannels();
                    if (channels is null || channels.Length == 0) return;

                    var totalCapacity = channels.Sum(c => c.Capacity.MilliSatoshi);
                    var totalLocalBalance = channels.Sum(c => c.LocalBalance.MilliSatoshi);
                    var totalInbound = totalCapacity - totalLocalBalance;
                    if (totalCapacity == 0) return;

                    var inboundPercent = (int)((totalInbound * 100) / totalCapacity);
                    var isLow = inboundPercent <= settings.LowLightningInboundThresholdPercent;
                    await HandleResult(
                        key: inboundKey,
                        title: $"Store '{store.StoreName}': Low Lightning inbound liquidity",
                        recoveryTitle: $"Store '{store.StoreName}': Lightning inbound liquidity restored",
                        message: isLow
                            ? $"Only {inboundPercent}% inbound capacity remaining. Incoming Lightning payments may start failing. Consider rebalancing or opening new channels."
                            : string.Empty,
                        status: isLow ? MonitorStatus.Warning : MonitorStatus.Healthy,
                        emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.CustomEmails,
                        customEmails: await GetOwnerEmails(store.Id, storeRepository),
                        serverAlertService: serverAlertService,
                        settingsRepository: settingsRepository);
                }
            }
            catch (Exception ex)
            {
                await HandleResult(
                    key: $"store:{store.Id}:lightning",
                    title: $"Store '{store.StoreName}': Lightning check failed",
                    recoveryTitle: $"Store '{store.StoreName}': Lightning check recovered",
                    message: $"An error occurred while checking Lightning: {ex.Message}",
                    status: MonitorStatus.Warning,
                    emailScope: settings.Delivery == AlertDelivery.BellOnly ? EmailScope.None : EmailScope.CustomEmails,
                    customEmails: await GetOwnerEmails(store.Id, storeRepository),
                    serverAlertService: serverAlertService,
                    settingsRepository: settingsRepository);
            }
        }
    }

    private async Task HandleResult(string key, string title, string recoveryTitle, string message, MonitorStatus status, EmailScope emailScope, string customEmails,
        ServerAlertService serverAlertService, SettingsRepository settingsRepository)
    {
        var isUnhealthy = status == MonitorStatus.Critical;
        var alreadyFired = _alertFired.TryGetValue(key, out var prev) && prev;
        logger.LogInformation("HealthMonitorService: HandleResult key={Key} status={Status} alreadyFired={AlreadyFired}", key, status, alreadyFired);
        if (isUnhealthy && !alreadyFired)
        {
            _alertFired[key] = true;
            await SendAlert(
                title: title,
                message: message,
                severity: status == MonitorStatus.Critical ? AnnouncementSeverity.Critical : AnnouncementSeverity.Warning,
                emailScope: emailScope,
                customEmails: customEmails, serverAlertService, settingsRepository);
        }
        else if (!isUnhealthy && alreadyFired)
        {
            _alertFired[key] = false;
            await SendAlert(
                title: recoveryTitle,
                message: $"{title} has been resolved.",
                severity: AnnouncementSeverity.Info,
                emailScope: emailScope,
                customEmails: customEmails, serverAlertService, settingsRepository);
        }
    }

    private async Task<string> GetOwnerEmails(string storeId, StoreRepository storeRepository)
    {
        var owners = await storeRepository.GetStoreUsers(storeId, filterRoles: new[] { StoreRoleId.Owner });
        return string.Join(",", owners.Where(o => !string.IsNullOrEmpty(o.Email)).Select(o => o.Email));
    }

    private async Task SendAlert(string title, string message, AnnouncementSeverity severity, EmailScope emailScope, string customEmails, 
        ServerAlertService serverAlertService, SettingsRepository settingsRepository)
    {
        var s = await settingsRepository.GetSettingAsync<ServerSettings>();
        var serverName = string.IsNullOrWhiteSpace(s?.ServerName) ? "BTCPay Server" : s.ServerName;
        await serverAlertService.CreateAndSendAnnouncement(new AlertSettings
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Message = message,
            Severity = severity,
            EmailScope = emailScope,
            CustomEmailAddresses = customEmails,
            CreatedAt = DateTimeOffset.UtcNow
        }, serverName);
    }

    private async Task WithScope(Func<ServerAlertService, StoreRepository, SettingsRepository, PullPaymentHostedService, Task> action)
    {
        using var scope = scopeFactory.CreateScope();
        var alertService = scope.ServiceProvider.GetRequiredService<ServerAlertService>();
        var storeRepo = scope.ServiceProvider.GetRequiredService<StoreRepository>();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<SettingsRepository>();
        var pullPaymentService = scope.ServiceProvider.GetRequiredService<PullPaymentHostedService>();
        await action(alertService, storeRepo, settingsRepo, pullPaymentService);
    }

    public class ServerCheckEvent
    {
        public ServerMonitorSettings Settings { get; set; }
    }

    public class StoreCheckEvent
    {
        public BTCPayServer.Data.StoreData Store { get; set; }
        public StoreMonitorSettings Settings { get; set; }
    }
}
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.ViewModels;

public class StoreMonitorViewModel
{
    public string StoreId { get; set; }
    public string StoreName { get; set; }
    public bool Enabled { get; set; }
    public bool AlertOnUnprocessedPayout { get; set; } = true;
    public int UnprocessedPayoutThresholdHours { get; set; } = 24;
    public bool AlertOnLightningNodeOffline { get; set; } = true;
    public bool AlertOnLowLightningInbound { get; set; } = true;
    public int LowLightningInboundThresholdPercent { get; set; } = 10;
    public AlertDelivery Delivery { get; set; } = AlertDelivery.BellOnly;

    public static StoreMonitorViewModel FromSettings(StoreMonitorSettings s, string storeId, string storeName) => new()
    {
        StoreId = storeId,
        StoreName = storeName,
        Enabled = s.Enabled,
        AlertOnUnprocessedPayout = s.AlertOnUnprocessedPayout,
        UnprocessedPayoutThresholdHours = s.UnprocessedPayoutThresholdHours,
        AlertOnLightningNodeOffline = s.AlertOnLightningNodeOffline,
        AlertOnLowLightningInbound = s.AlertOnLowLightningInbound,
        LowLightningInboundThresholdPercent = s.LowLightningInboundThresholdPercent,
        Delivery = s.Delivery
    };

    public StoreMonitorSettings ToSettings() => new()
    {
        Enabled = Enabled,
        AlertOnUnprocessedPayout = AlertOnUnprocessedPayout,
        UnprocessedPayoutThresholdHours = UnprocessedPayoutThresholdHours,
        AlertOnLightningNodeOffline = AlertOnLightningNodeOffline,
        AlertOnLowLightningInbound = AlertOnLowLightningInbound,
        LowLightningInboundThresholdPercent = LowLightningInboundThresholdPercent,
        Delivery = Delivery
    };
}

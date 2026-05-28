using System;
using System.Collections.Generic;
using System.Text;

namespace BTCPayServer.Plugins.ServerAlert.Data;

public class ServerMonitorSettings
{
    public bool Enabled { get; set; } = false;
    public bool MonitorBitcoinNode { get; set; } = true;
    public AlertDelivery Delivery { get; set; } = AlertDelivery.BellOnly;
    public List<MonitorAlertLog> RecentAlerts { get; set; } = new();
}

public class StoreMonitorSettings
{
    public bool Enabled { get; set; } = false;
    public bool AlertOnUnprocessedPayout { get; set; } = true;
    public bool AlertOnChannelClose { get; set; } = true;
    public int UnprocessedPayoutThresholdHours { get; set; } = 24;
    public bool AlertOnLightningNodeOffline { get; set; } = true;
    public bool AlertOnLowLightningInbound { get; set; } = true;
    public int LowLightningInboundThresholdPercent { get; set; } = 10;

    public AlertDelivery Delivery { get; set; } = AlertDelivery.BellOnly;
}

public class MonitorAlertLog
{
    public string CheckName { get; set; }
    public string Message { get; set; }
    public MonitorStatus Status { get; set; }
    public DateTimeOffset FiredAt { get; set; }
}

public enum AlertDelivery { BellOnly, EmailOnly, BothBellAndEmail }
public enum MonitorStatus { Healthy, Warning, Critical }
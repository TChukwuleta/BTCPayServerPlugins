using System.Collections.Generic;
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.ViewModels;

public class ServerMonitorViewModel
{
    public bool Enabled { get; set; }
    public bool MonitorBitcoinNode { get; set; } = true;
    public AlertDelivery Delivery { get; set; } = AlertDelivery.BothBellAndEmail;
    public List<MonitorAlertLog> RecentAlerts { get; set; } = new();

    public static ServerMonitorViewModel FromSettings(ServerMonitorSettings s) => new()
    {
        Enabled = s.Enabled,
        MonitorBitcoinNode = s.MonitorBitcoinNode,
        Delivery = s.Delivery,
        RecentAlerts = s.RecentAlerts ?? new()
    };

    public ServerMonitorSettings ToSettings() => new()
    {
        Enabled = Enabled,
        MonitorBitcoinNode = MonitorBitcoinNode,
        Delivery = Delivery,
        RecentAlerts = RecentAlerts
    };
}

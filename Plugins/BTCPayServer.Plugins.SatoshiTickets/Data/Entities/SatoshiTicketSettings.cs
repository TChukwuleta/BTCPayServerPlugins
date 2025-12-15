namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class SatoshiTicketSettings
{
    public bool EnableEventAutoReminder { get; set; }
    public int EventReminderDaysBefore { get; set; } = 1;
}

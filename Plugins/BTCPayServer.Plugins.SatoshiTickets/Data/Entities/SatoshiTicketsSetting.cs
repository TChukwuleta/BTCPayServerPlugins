using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.SatoshiTickets.Data;

public class SatoshiTicketsSetting
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string StoreId { get; set; }
    public bool EnableAutoReminders { get; set; }
    public int DefaultReminderDaysBeforeEvent { get; set; }
    public string ReminderEmailSubject { get; set; }
    public string ReminderEmailBody { get; set; }
}

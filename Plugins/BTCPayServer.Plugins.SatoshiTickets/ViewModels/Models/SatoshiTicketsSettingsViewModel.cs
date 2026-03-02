using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.SatoshiTickets.ViewModels.Models;

public class SatoshiTicketsSettingsViewModel
{
    [Display(Name = "Enable auto reminders for all satoshi ticket events in this store")]
    public bool EnableAutoReminders { get; set; }

    [Display(Name = "Default days before event to send reminder")]
    [Range(1, 365, ErrorMessage = "Must be between 1 and 365 days")]
    public int DefaultReminderDaysBeforeEvent { get; set; } = 3;

    [Display(Name = "Reminder Email Subject")] 
    public string ReminderEmailSubject { get; set; }

    [Display(Name = "Reminder Email Body")]
    public string ReminderEmailBody { get; set; }
}

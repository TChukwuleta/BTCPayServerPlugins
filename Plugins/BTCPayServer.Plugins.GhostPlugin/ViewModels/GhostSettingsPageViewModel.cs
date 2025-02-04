using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class GhostSettingsPageViewModel
{

    [Display(Name = "Allow subscription emails to be sent to members")]
    public bool AllowSubscriptionEmail { get; set; }
    public string SubscriptionEmailSubject { get; set; }
    public string SubscriptionEmailBody { get; set; }

    [Display(Name = "Send Ghost member automated subscription email reminder")]
    public bool SendAutomatedSubscriptionRemider { get; set; }

    public ReminderDaysEnum ReminderDays { get; set; } 
    public int? CustomReminderDays { get; set; }

    public string StoreId { get; set; }
}

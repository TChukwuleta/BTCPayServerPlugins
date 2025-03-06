﻿using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.GhostPlugin.ViewModels;

public class GhostSettingsPageViewModel
{
    [Display(Name = "Enable automated email reminders for expiring subscriptions")]
    public bool EnableAutomatedEmailReminders { get; set; }

    [Display(Name = "Number of days before expiration to start sending reminders")]
    public int? ReminderStartDaysBeforeExpiration { get; set; }
}

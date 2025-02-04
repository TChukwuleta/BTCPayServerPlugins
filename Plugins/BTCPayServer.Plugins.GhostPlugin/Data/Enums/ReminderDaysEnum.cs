using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.GhostPlugin.Data;

public enum ReminderDaysEnum
{
    [Display(Name = "The same day")]
    SameDay = 0,

    [Display(Name = "1 Day Before")]
    OneDay = 1,

    [Display(Name = "3 Days Before")]
    ThreeDays = 3,

    [Display(Name = "7 Days Before")]
    SevenDays = 7,

    [Display(Name = "Custom")]
    Custom = -1
}

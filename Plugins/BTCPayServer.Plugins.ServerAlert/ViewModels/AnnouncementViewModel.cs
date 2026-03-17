using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BTCPayServer.Data;
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.ViewModels;

public class AnnouncementViewModel
{
    public string Id { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "Notification Title")]
    public string Title { get; set; }

    [Required]
    [Display(Name = "Notification Message")]
    public string Message { get; set; }
    public AnnouncementSeverity Severity { get; set; }
    public bool IsPublished { get; set; }
    public bool BellNotificationsSent { get; set; }

    [Display(Name = "Email notifications")]
    public EmailScope EmailScope { get; set; } = EmailScope.None;
    public List<string> SelectedStoreIds { get; set; } = new();

    [Display(Name = "Email addresses (one per line)")]
    public string? CustomEmailAddresses { get; set; }
    public bool EmailsSent { get; set; }
    public int EmailsSentCount { get; set; }
    public bool EmailEnabled { get; set; }
    public List<StoreData> AllStores { get; set; } = new();

    public static AnnouncementViewModel FromEntity(AlertSettings a, List<StoreData>? stores = null) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Message = a.Message,
        Severity = a.Severity,
        EmailScope = a.EmailScope,
        SelectedStoreIds = a.GetSelectedStoreIds(),
        CustomEmailAddresses = a.CustomEmailAddresses,
        AllStores = stores ?? new()
    };

    public AlertSettings ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid().ToString(),
        Title = Title,
        Message = Message,
        Severity = Severity,
        EmailScope = EmailScope,
        SelectedStoreIds = SelectedStoreIds.Any() ? string.Join(",", SelectedStoreIds) : null,
        CustomEmailAddresses = CustomEmailAddresses?.Trim()
    };
}

public class HeraldIndexViewModel
{
    public List<AnnouncementViewModel> Announcements { get; set; } = new();
    public bool EmailEnabled { get; set; } = true;
}
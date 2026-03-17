using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.ServerAlert.Data;

public class Announcement
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public AnnouncementSeverity Severity { get; set; }
    public bool IsPublished { get; set; }
    public bool BellNotificationsSent { get; set; }
    public EmailScope EmailScope { get; set; } = EmailScope.None;
    public string? SelectedStoreIds { get; set; } // used when EmailScope = SelectedStores
    public string? CustomEmailAddresses { get; set; } // used when EmailScope = CustomEmails
    public bool EmailsSent { get; set; }
    public int EmailsSentCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<string> GetSelectedStoreIds() =>
        string.IsNullOrWhiteSpace(SelectedStoreIds) ? new() : new(SelectedStoreIds.Split(',',  StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public List<string> GetCustomEmails() =>
        string.IsNullOrWhiteSpace(CustomEmailAddresses) ? new() : new(CustomEmailAddresses.Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public enum AnnouncementSeverity { Info, Warning, Critical }

public enum EmailScope { None, AllUsers, AdminsOnly, AllStores, SelectedStores, CustomEmails }// None means no email — bell notifications only.

public enum ServerAlertNavPages { ServerAlertIndex, ServerAlertSettings }
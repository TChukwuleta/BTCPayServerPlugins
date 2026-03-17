using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.Services;

public class ServerAlertAnnouncement : BaseNotification
{
    private const string TYPE = "serveralert_announcement";

    public ServerAlertAnnouncement() { }

    public ServerAlertAnnouncement(string announcementId, string title, string message, AnnouncementSeverity severity)
    {
        AnnouncementId = announcementId;
        Title = title;
        Message = message;
        Severity = severity;
    }

    public string AnnouncementId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AnnouncementSeverity Severity { get; set; }

    public override string Identifier => TYPE;
    public override string NotificationType => TYPE;
}

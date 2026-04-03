using System;
using System.Net;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.Services;

internal class ServerAlertNotificationHandler : INotificationHandler
{
    public const string NotificationType = "serveralert_announcement";
    string INotificationHandler.NotificationType => NotificationType;

    public Type NotificationBlobType => typeof(ServerAlertAnnouncement);

    public (string identifier, string name)[] Meta =>  new[] { (NotificationType, "Server Alert") };

    public void FillViewModel(object notification, NotificationViewModel vm)
    {
        if (notification is not ServerAlertAnnouncement data) return;

        var severityPrefix = data.Severity switch
        {
            AnnouncementSeverity.Critical => "CRITICAL — ",
            AnnouncementSeverity.Warning => "WARNING — ",
            _ => ""
        };
        vm.Identifier = NotificationType;
        vm.Body = severityPrefix + $"{WebUtility.HtmlEncode(data.Title)}: {WebUtility.HtmlEncode(Truncate(data.Message, 100))}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

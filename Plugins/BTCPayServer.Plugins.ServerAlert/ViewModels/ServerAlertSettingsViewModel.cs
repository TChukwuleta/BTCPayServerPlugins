using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.ServerAlert.Data;

namespace BTCPayServer.Plugins.ServerAlert.ViewModels;

public class ServerAlertSettingsViewModel
{
    [Display(Name = "From email address")]
    [EmailAddress]
    public string? FromEmail { get; set; }

    [Display(Name = "From display name")]
    [MaxLength(200)]
    public string? FromName { get; set; }

    public static ServerAlertSettingsViewModel FromSettings(ServerAlertSettings? s) => new()
    {
        FromEmail = s?.FromEmail,
        FromName = s?.FromName
    };

    public ServerAlertSettings ToSettings() => new()
    {
        FromEmail = FromEmail,
        FromName = FromName
    };
}

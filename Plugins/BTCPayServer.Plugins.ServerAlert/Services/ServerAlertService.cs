using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.ServerAlert.Data;
using BTCPayServer.Plugins.ServerAlert.ViewModels;
using BTCPayServer.Services;
using BTCPayServer.Services.Notifications;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace BTCPayServer.Plugins.ServerAlert.Services;

public class ServerAlertService(StoreRepository storeRepository, 
        EmailSenderFactory emailSenderFactory,
        SettingsRepository settingsRepository,
        NotificationSender notificationSender,
        UserManager<ApplicationUser> userManager)
{
    public async Task<AlertSettings?> GetAnnouncement(string id)
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        return serverAlert?.Alerts.FirstOrDefault(a => a.Id == id);
    }

    public async Task<List<AnnouncementViewModel>> GetAllAnnouncement()
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        var alertSettings = serverAlert.Alerts ?? new List<AlertSettings>();
        return alertSettings.OrderByDescending(c => c.CreatedAt).Select(c => new AnnouncementViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Message = c.Message,
            Severity = c.Severity,
            IsPublished = c.IsPublished,
            BellNotificationsSent = c.BellNotificationsSent,
            EmailScope = c.EmailScope,
            EmailsSent = c.EmailsSent,
            EmailsSentCount = c.EmailsSentCount
        }).ToList();
    }

    public async Task<PublishResult> CreateAndSendAnnouncement(AlertSettings entity, string serverName)
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        serverAlert.Alerts ??= new List<AlertSettings>();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        serverAlert.Alerts.Add(entity);
        await settingsRepository.UpdateSetting(serverAlert);
        return await SendAlerts(serverAlert, entity, serverName);
    }

    public async Task<bool> UpdateAnnouncement(AnnouncementViewModel vm)
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        serverAlert.Alerts ??= new List<AlertSettings>();
        var existingAlert = serverAlert.Alerts.FirstOrDefault(a => a.Id == vm.Id);
        if (existingAlert is null) return false;

        existingAlert.Title = vm.Title;
        existingAlert.Message = vm.Message;
        existingAlert.Severity = vm.Severity;
        existingAlert.EmailScope = vm.EmailScope;
        existingAlert.UpdatedAt = DateTimeOffset.UtcNow;
        existingAlert.SelectedStoreIds = vm.EmailScope == EmailScope.SelectedStores ? string.Join(',', vm.SelectedStoreIds) : null;
        existingAlert.CustomEmailAddresses = vm.CustomEmailAddresses?.Trim();
        await settingsRepository.UpdateSetting(serverAlert);
        return true;
    }

    public async Task DeleteAnnouncement(string Id)
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        serverAlert.Alerts ??= new List<AlertSettings>();
        var existingAlert = serverAlert.Alerts.FirstOrDefault(a => a.Id == Id);
        if (existingAlert is not null)
        {
            serverAlert.Alerts.Remove(existingAlert);
            await settingsRepository.UpdateSetting(serverAlert);
        }
    }

    public async Task<PublishResult> RepublishAnnouncement(string Id, string serverName)
    {
        var serverAlert = await settingsRepository.GetSettingAsync<ServerAlertSettings>() ?? new();
        serverAlert.Alerts ??= new List<AlertSettings>();
        var existingAlert = serverAlert.Alerts.FirstOrDefault(a => a.Id == Id);

        if (existingAlert is null) return new PublishResult { Found = false };

        existingAlert.BellNotificationsSent = false;
        existingAlert.EmailsSent = false;
        existingAlert.EmailsSentCount = 0;
        await settingsRepository.UpdateSetting(serverAlert);
        return await SendAlerts(serverAlert, existingAlert, serverName);
    }

    private async Task<PublishResult> SendAlerts(ServerAlertSettings serverAlert, AlertSettings entity, string serverName)
    {
        var result = new PublishResult { Found = true, EmailScopeWasSet = entity.EmailScope != EmailScope.None };
        entity.IsPublished = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (!entity.BellNotificationsSent)
        {
            result.BellCount = await DispatchBellNotifications(entity);
            entity.BellNotificationsSent = true;
        }

        if (!entity.EmailsSent && entity.EmailScope != EmailScope.None)
        {
            result.EmailCount = await DispatchEmailNotifications(entity, serverName);
            entity.EmailsSent = true;
            entity.EmailsSentCount = result.EmailCount;
        }
        await settingsRepository.UpdateSetting(serverAlert);
        return result;
    }

    private async Task<int> DispatchBellNotifications(AlertSettings entity)
    {
        var blob = new ServerAlertAnnouncement(entity.Id, entity.Title, entity.Message, entity.Severity);
        var userIds = await GetScopedUserIds(entity);
        int sent = 0;
        foreach (var userId in userIds)
        {
            try
            {
                await notificationSender.SendNotification(new UserScope(userId), blob);
                sent++;
            }
            catch (Exception) { }
        }
        return sent;
    }

    private async Task<List<string>> GetScopedUserIds(AlertSettings entity)
    {
        return entity.EmailScope switch
        {
            EmailScope.AllUsers => await userManager.Users.Select(u => u.Id).ToListAsync(),

            EmailScope.AdminsOnly => (await userManager.GetUsersInRoleAsync(Roles.ServerAdmin))
                .Select(u => u.Id).ToList(),

            EmailScope.SelectedStores => (await Task.WhenAll(
                entity.GetSelectedStoreIds().Select(id =>
                    storeRepository.GetStoreUsers(id, filterRoles: new[] { StoreRoleId.Owner }))))
                .SelectMany(owners => owners.Select(o => o.Id))
                .Distinct().ToList(),

            EmailScope.AllStores => (await Task.WhenAll(
                (await storeRepository.GetStores()).Select(s =>
                    storeRepository.GetStoreUsers(s.Id, filterRoles: new[] { StoreRoleId.Owner }))))
                .SelectMany(owners => owners.Select(o => o.Id))
                .Distinct().ToList(),

            _ => await userManager.Users.Select(u => u.Id).ToListAsync()
        };
    }

    private async Task<int> DispatchEmailNotifications(AlertSettings entity, string serverName)
    {
        var emailSender = await emailSenderFactory.GetEmailSender();
        if (emailSender is null) return 0;

        var emailSettings = await emailSender.GetEmailSettings();
        if (emailSettings is null || !emailSettings.IsComplete()) return 0;

        List<MailboxAddress> bccList = await BuildRecipientList(entity);
        if (bccList.Count == 0) return 0;

        var subject = $"[{entity.Severity.ToString().ToUpperInvariant()}] {entity.Title}";
        var undisclosed = new MailboxAddress("undisclosed-recipients", emailSettings.From);
        try
        {
            emailSender.SendEmail(email: new[] { undisclosed }, 
                cc: Array.Empty<MailboxAddress>(), 
                bcc: bccList.ToArray(), 
                subject: subject, 
                message: BuildEmailBody(entity, serverName));
            return bccList.Count;
        }
        catch (Exception){ return 0; }
    }

    private async Task<List<MailboxAddress>> BuildRecipientList(AlertSettings entity)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (entity.EmailScope)
        {
            case EmailScope.AllUsers:
                (await userManager.Users.Where(u => u.Email != null).Select(u => u.Email!).ToListAsync()).ForEach(e => emails.Add(e));
                break;

            case EmailScope.AdminsOnly:
                (await userManager.GetUsersInRoleAsync(Roles.ServerAdmin)).Where(u => !string.IsNullOrEmpty(u.Email)).ToList().ForEach(u => emails.Add(u.Email!));
                break;

            case EmailScope.SelectedStores:
                foreach (var storeId in entity.GetSelectedStoreIds())
                {
                    var owners = await storeRepository.GetStoreUsers(storeId, filterRoles: new[] { StoreRoleId.Owner });
                    owners.Where(o => !string.IsNullOrEmpty(o.Email)).ToList().ForEach(o => emails.Add(o.Email));
                }
                break;

            case EmailScope.AllStores:
                    var allStores = await storeRepository.GetStores();
                    foreach (var store in allStores)
                    {
                        var owners = await storeRepository.GetStoreUsers(store.Id, filterRoles: new[] { StoreRoleId.Owner });
                        owners.Where(o => !string.IsNullOrEmpty(o.Email)) .ToList() .ForEach(o => emails.Add(o.Email));
                    }
                    break;

            case EmailScope.CustomEmails:
                entity.GetCustomEmails().ForEach(e => emails.Add(e));
                break;
        }
        return emails.Select(e => { try { return MailboxAddress.Parse(e); } catch { return null; } })
            .Where(m => m is not null).Select(m => m!).ToList();
    }

    public async Task<bool> IsEmailSettingsConfigured()
    {
        var emailSender = await emailSenderFactory.GetEmailSender();
        return (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
    }

    public string BuildEmailBody(AlertSettings entity, string serverName)
    {
        var title = System.Net.WebUtility.HtmlEncode(entity.Title);
        var message = System.Net.WebUtility.HtmlEncode(entity.Message).Replace("\n", "<br>");
        var server = System.Net.WebUtility.HtmlEncode(serverName);
        var severity = entity.Severity.ToString().ToUpperInvariant();

        return $"BTCPay Server Alert — {server}<br><br>" +
               $"[{severity}] {title}<br><br>" +
               $"{message}<br><br>" +
               $"You received this because you have an account on {server}.";
    }
}

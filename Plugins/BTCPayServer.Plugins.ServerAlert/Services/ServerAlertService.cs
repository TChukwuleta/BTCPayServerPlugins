using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.LightSpeed.Data;
using BTCPayServer.Plugins.ServerAlert.Data;
using BTCPayServer.Plugins.ServerAlert.ViewModels;
using BTCPayServer.Services.Notifications;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace BTCPayServer.Plugins.ServerAlert.Services;

public class ServerAlertService(StoreRepository storeRepository, 
        EmailSenderFactory emailSenderFactory,
        NotificationSender notificationSender,
        UserManager<ApplicationUser> userManager,
        ServerAlertDbContextFactory dbContextFactory)
{
    public async Task<Announcement> GetAnnouncement(string id)
    {
        await using var ctx = dbContextFactory.CreateContext();
        return ctx.Announcements.Find(id);
    }

    public async Task<List<AnnouncementViewModel>> GetAllAnnouncement()
    {
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Announcements.OrderByDescending(c => c.CreatedAt).ToList();
        return entity.Select(c => new AnnouncementViewModel
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

    public async Task<List<AnnouncementViewModel>> GetPublishedAnnouncement()
    {
        await using var ctx = dbContextFactory.CreateContext();
        var entity = ctx.Announcements.Where(c => c.IsPublished).OrderByDescending(c => c.CreatedAt).ToList();
        return entity.Select(c => new AnnouncementViewModel
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

    public async Task<PublishResult> CreateAndSendAnnouncement(Announcement entity, string serverName)
    {
        await using var ctx = dbContextFactory.CreateContext();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        ctx.Announcements.Add(entity);
        await ctx.SaveChangesAsync();
        return await SendAlerts(entity, ctx, serverName);
    }

    public async Task<bool> UpdateAnnouncement(AnnouncementViewModel vm)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var existingAnnouncement = ctx.Announcements.Find(vm.Id);
        if (existingAnnouncement is null) return false;

        existingAnnouncement.Title = vm.Title;
        existingAnnouncement.Message = vm.Message;
        existingAnnouncement.Severity = vm.Severity;
        existingAnnouncement.EmailScope = vm.EmailScope;
        existingAnnouncement.UpdatedAt = DateTimeOffset.UtcNow;
        existingAnnouncement.SelectedStoreIds = vm.EmailScope == EmailScope.SelectedStores ? string.Join(',', vm.SelectedStoreIds) : null;
        existingAnnouncement.CustomEmailAddresses = vm.CustomEmailAddresses?.Trim();
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task DeleteAnnouncement(string Id)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var existingAnnouncement = ctx.Announcements.Find(Id);
        if (existingAnnouncement is null) return;

        ctx.Announcements.Remove(existingAnnouncement);
        await ctx.SaveChangesAsync();
    }

    public async Task<PublishResult> RepublishAnnouncement(string Id, string serverName)
    {
        await using var ctx = dbContextFactory.CreateContext();
        var existingAnnouncement = ctx.Announcements.Find(Id);
        if (existingAnnouncement is null) return new PublishResult { Found = false };

        existingAnnouncement.BellNotificationsSent = false;
        existingAnnouncement.EmailsSent = false;
        existingAnnouncement.EmailsSentCount = 0;
        await ctx.SaveChangesAsync();

        return await SendAlerts(existingAnnouncement, ctx, serverName);
    }

    private async Task<PublishResult> SendAlerts(Announcement entity, ServerAlertDbContext ctx, string serverName)
    {
        var result = new PublishResult { Found = true, EmailScopeWasSet = entity.EmailScope != EmailScope.None };

        if (!entity.BellNotificationsSent)
        {
            result.BellCount = await DispatchBellNotifications(entity);
            entity.IsPublished = true;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.BellNotificationsSent = true;
            await ctx.SaveChangesAsync();
        }

        if (!entity.EmailsSent && entity.EmailScope != EmailScope.None)
        {
            result.EmailCount = await DispatchEmailNotifications(entity, serverName);
            entity.IsPublished = true;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.EmailsSent = true;
            entity.EmailsSentCount = result.EmailCount;
            await ctx.SaveChangesAsync();
        }
        return result;
    }

    private async Task<int> DispatchBellNotifications(Announcement entity)
    {
        var blob = new ServerAlertAnnouncement(entity.Id, entity.Title, entity.Message, entity.Severity);
        var userIds = await userManager.Users.Select(u => u.Id).ToListAsync();
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

    private async Task<int> DispatchEmailNotifications(Announcement entity, string serverName)
    {
        var emailSender = await emailSenderFactory.GetEmailSender();
        if (emailSender is null) return 0;

        var emailSettings = await emailSender.GetEmailSettings();
        if (emailSettings is null || !emailSettings.IsComplete()) return 0;

        await using var ctx = dbContextFactory.CreateContext();
        List<MailboxAddress> bccList = await BuildRecipientList(entity, ctx);
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

    private async Task<List<MailboxAddress>> BuildRecipientList(Announcement entity, ServerAlertDbContext ctx)
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

    public string BuildEmailBody(Announcement entity, string serverName)
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

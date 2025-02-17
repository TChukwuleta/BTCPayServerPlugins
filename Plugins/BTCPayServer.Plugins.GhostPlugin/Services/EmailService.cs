using System.Threading.Tasks;
using System;
using BTCPayServer.Logging;
using BTCPayServer.Services.Mails;
using MimeKit;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.GhostPlugin.Data;

namespace BTCPayServer.Plugins.GhostPlugin.Services;

public class EmailService
{
    private readonly EmailSenderFactory _emailSender;
    private readonly Logs _logs;
    public EmailService(EmailSenderFactory emailSender, Logs logs)
    {
        _logs = logs;
        _emailSender = emailSender;
    }

    private async Task SendBulkEmail(string storeId, IEnumerable<EmailRecipient> recipients)
    {
        var settings = await (await _emailSender.GetEmailSender(storeId)).GetEmailSettings();
        if (!settings.IsComplete())
            return;

        var client = await settings.CreateSmtpClient();
        try
        {
            foreach (var recipient in recipients)
            {
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(MailboxAddress.Parse(settings.From));
                    message.To.Add(recipient.Address);
                    message.Subject = recipient.Subject;
                    message.Body = new TextPart("plain") { Text = recipient.MessageText };
                    await client.SendAsync(message);
                }
                catch (Exception ex)
                {
                    _logs.PayServer.LogError(ex, $"Error sending email to: {recipient.Address}");
                }
            }
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }

    public async Task SendTicketRegistrationEmail(string storeId, GhostEventTicket ticket, GhostEvent ghostEvent)
    {
        string emailBody = ghostEvent.EmailBody
                            .Replace("{{Title}}", ghostEvent.Title)
                            .Replace("{{EventLink}}", ghostEvent.EventLink)
                            .Replace("{{Description}}", ghostEvent.Description)
                            .Replace("{{EventDate}}", ghostEvent.EventDate.ToString("MMMM dd, yyyy"))
                            .Replace("{{Amount}}", ghostEvent.Amount.ToString())
                            .Replace("{{Currency}}", ghostEvent.Currency);

        var emailRecipients = new List<EmailRecipient>
        {
            new EmailRecipient
            {
                Address = InternetAddress.Parse(ticket.Email),
                Subject = ghostEvent.EmailSubject,
                MessageText = ghostEvent.EmailBody
            }
        };
        await SendBulkEmail(storeId, emailRecipients);
    }

    public async Task SendMembershipSubscriptionReminderEmail(EmailRequest model)
    {
        var settings = await (await _emailSender.GetEmailSender(model.StoreId)).GetEmailSettings();
        if (!settings.IsComplete())
            return;

        var templateContent = GetEmbeddedResourceContent("Templates.SubscriptionExpirationReminder.cshtml");
        string emailBody = templateContent
                            .Replace("@Model.MemberName", model.MemberName)
                            .Replace("@Model.SubscriptionTier", model.SubscriptionTier)
                            .Replace("@Model.ExpirationDate", model.ExpirationDate.ToString("MMMM dd, yyyy"))
                            .Replace("@Model.SubscriptionUrl", model.SubscriptionUrl)
                            .Replace("@Model.StoreName", model.StoreName)
                            .Replace("@Model.ApiUrl", $"https://{model.ApiUrl}");
        var client = await settings.CreateSmtpClient();
        var clientMessage = new MimeMessage
        {
            Subject = "Your Ghost Subscription is Expiring Soon!",
            Body = new BodyBuilder
            {
                HtmlBody = emailBody,
                TextBody = StripHtml(emailBody)
            }.ToMessageBody()
        };
        clientMessage.From.Add(MailboxAddress.Parse(settings.From));
        clientMessage.To.Add(InternetAddress.Parse(model.MemberEmail));
        await client.SendAsync(clientMessage);
        await client.DisconnectAsync(true);
    }


    public string GetEmbeddedResourceContent(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = assembly.GetManifestResourceNames()
                                       .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (fullResourceName == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found in assembly.");
        }
        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
            .Replace("&nbsp;", " ")
            .Trim();
    }

    public class EmailRecipient
    {
        public InternetAddress Address { get; set; }
        public string Subject { get; set; }
        public string MessageText { get; set; }
    }

    public class EmailRequest
    {
        public string StoreId { get; set; }
        public string MemberId { get; set; }
        public string MemberName { get; set; }
        public string MemberEmail { get; set; }
        public string SubscriptionTier { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string SubscriptionUrl { get; set; }
        public string StoreName { get; set; }
        public string ApiUrl { get; set; }
    }
}

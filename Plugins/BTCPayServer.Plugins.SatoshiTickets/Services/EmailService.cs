using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BTCPayServer.Plugins.SatoshiTickets.Services;

public class EmailService
{
    private readonly EmailSenderFactory _emailSender;
    private readonly Logging.Logs _logs;
    public EmailService(EmailSenderFactory emailSender, Logging.Logs logs)
    {
        _logs = logs;
        _emailSender = emailSender;
    }

    public async Task<bool> IsEmailSettingsConfigured(string storeId)
    {
        var emailSender = await _emailSender.GetEmailSender(storeId);
        return (await emailSender.GetEmailSettings() ?? new EmailSettings()).IsComplete();
    }

    private async Task<EmailDispatchResult> SendBulkEmail(string storeId, IEnumerable<EmailRecipient> recipients)
    {
        var settings = await (await _emailSender.GetEmailSender(storeId)).GetEmailSettings();
        if (!settings.IsComplete())
            return new EmailDispatchResult { IsSuccessful = false };

        var recipientList = recipients.ToList();
        if (recipientList.Count == 0)
            return new EmailDispatchResult { IsSuccessful = true };

        var failedRecipients = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(recipientList, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (recipient, _) =>
        {
            using var client = await settings.CreateSmtpClient();
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
                failedRecipients.Add(recipient.Address.ToString());
                _logs.PayServer.LogError(ex, $"Error sending email to: {recipient.Address}");
            }
            finally
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true);
            }
        });
        var failed = failedRecipients.ToList();
        return new EmailDispatchResult { IsSuccessful = failed.Count == 0, FailedRecipients = failed };
    }


    private async Task<EmailDispatchResult> SendBulkEmail2(string storeId, IEnumerable<EmailRecipient> recipients)
    {
        var settings = await (await _emailSender.GetEmailSender(storeId)).GetEmailSettings();
        if (!settings.IsComplete())
            return new EmailDispatchResult { IsSuccessful = false };

        var failedRecipients = new List<string>();
        var isSuccess = true;
        SmtpClient client = null;
        try
        {
            client = await settings.CreateSmtpClient();
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
                    isSuccess = false;
                    failedRecipients.Add(recipient.Address.ToString());
                    _logs.PayServer.LogError(ex, $"Error sending email to: {recipient.Address}");
                }
            }
        }
        catch (Exception ex)
        {
            _logs.PayServer.LogError(ex, "Failed to establish SMTP connection for bulk email");
            return new EmailDispatchResult { IsSuccessful = false, FailedRecipients = recipients.Select(r => r.Address.ToString()).ToList() };
        }
        finally
        {
            if (client != null)
            {
                try
                {
                    if (client.IsConnected)
                        await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    _logs.PayServer.LogError(ex, "Error disconnecting SMTP client");
                }
                client.Dispose();
            }
        }
        return new EmailDispatchResult { IsSuccessful = isSuccess, FailedRecipients = failedRecipients };
    }

    public async Task<EmailDispatchResult> SendTicketRegistrationEmail(string storeId, Ticket ticket, Event ticketEvent)
    {
        var recipients = new List<EmailRecipient>();
        string emailBody = ticketEvent.EmailBody
                            .Replace("{{Title}}", ticketEvent.Title)
                            .Replace("{{Location}}", ticketEvent.Location)
                            .Replace("{{Name}}", $"{ticket.FirstName} {ticket.LastName}")
                            .Replace("{{Email}}", ticket.Email)
                            .Replace("{{Description}}", ticketEvent.Description)
                            .Replace("{{EventDate}}", ticketEvent.StartDate.ToString("MMMM dd, yyyy"))
                            .Replace("{{Currency}}", ticketEvent.Currency);

        emailBody = @$" {emailBody}

Click the link to view your tickets: {ticket.QRCodeLink}";

        recipients.Add(new EmailRecipient
        {
            Address = InternetAddress.Parse(ticket.Email),
            Subject = ticketEvent.EmailSubject,
            MessageText = emailBody
        });
        return await SendBulkEmail(storeId, recipients);
    }

    public async Task SendTicketRegistrationEmail(string storeId, IEnumerable<Ticket> tickets, Event ticketEvent)
    {
        var recipients = new List<EmailRecipient>();
        foreach (var ticket in tickets)
        {
            string emailBody = ticketEvent.EmailBody
                .Replace("{{Title}}", ticketEvent.Title)
                .Replace("{{Location}}", ticketEvent.Location)
                .Replace("{{Name}}", $"{ticket.FirstName} {ticket.LastName}")
                .Replace("{{Email}}", ticket.Email)
                .Replace("{{Description}}", ticketEvent.Description)
                .Replace("{{EventDate}}", ticketEvent.StartDate.ToString("MMMM dd, yyyy"))
                .Replace("{{Currency}}", ticketEvent.Currency);

            emailBody = @$"{emailBody}

Click the link to view your tickets: {ticket.QRCodeLink}";

            try
            {
                recipients.Add(new EmailRecipient
                {
                    Address = InternetAddress.Parse(ticket.Email),
                    Subject = ticketEvent.EmailSubject,
                    MessageText = emailBody
                });
            }
            catch (Exception ex)
            {
                _logs.PayServer.LogWarning(ex, $"Invalid email for ticket {ticket.Id}: {ticket.Email}");
            }
        }
        await SendBulkEmail(storeId, recipients);
    }

    public async Task<bool> SendReminderEmail(string storeId, IEnumerable<Ticket> uniqueTickets, Event ticketEvent, string reminderSubject, string reminderBody)
    {
        var recipients = new List<EmailRecipient>();
        var subject = !string.IsNullOrWhiteSpace(reminderSubject) ? reminderSubject : ticketEvent.EmailSubject;
        var bodyTemplate = !string.IsNullOrWhiteSpace(reminderBody) ? reminderBody : ticketEvent.EmailBody;
        foreach (var ticket in uniqueTickets)
        {
            string emailBody = bodyTemplate
                .Replace("{{Title}}", ticketEvent.Title)
                .Replace("{{Location}}", ticketEvent.Location)
                .Replace("{{Name}}", $"{ticket.FirstName} {ticket.LastName}")
                .Replace("{{Email}}", ticket.Email)
                .Replace("{{Description}}", ticketEvent.Description)
                .Replace("{{EventDate}}", ticketEvent.StartDate.ToString("MMMM dd, yyyy"))
                .Replace("{{Currency}}", ticketEvent.Currency);

            try
            {
                recipients.Add(new EmailRecipient
                {
                    Address = InternetAddress.Parse(ticket.Email),
                    Subject = subject,
                    MessageText = emailBody
                });
            }
            catch (Exception ex)
            {
                _logs.PayServer.LogWarning(ex, $"Invalid email for ticket {ticket.Id}: {ticket.Email}");
                return false;
            }
        }
        var sendBultEmail = await SendBulkEmail(storeId, recipients);
        return sendBultEmail.IsSuccessful;
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

    public class EmailRecipient
    {
        public InternetAddress Address { get; set; }
        public string Subject { get; set; }
        public string MessageText { get; set; }
    }

    public class EmailDispatchResult
    {
        public List<string> FailedRecipients { get; set; } = new();
        public bool IsSuccessful { get; set; }
    }
}

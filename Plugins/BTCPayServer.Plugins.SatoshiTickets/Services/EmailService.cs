using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Emails.Services;
using BTCPayServer.Plugins.SatoshiTickets.Data;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using Org.BouncyCastle.Cms;
using static BTCPayServer.Plugins.Monetization.Views.SelectExistingOfferingModalViewModel;

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

    public async Task<EmailDispatchResult> SendTicketRegistrationEmail(string storeId, Ticket ticket, Event ticketEvent, string customEmail = null)
    {
        var recipientAddress = string.IsNullOrWhiteSpace(customEmail) ? ticket.Email : customEmail.Trim();
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
            Address = InternetAddress.Parse(recipientAddress),
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

            emailBody = ApplyOrderFinancials(emailBody, null, ticketEvent.Currency);
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

    public async Task<EmailDispatchResult> SendReferrerInvitationEmail(string storeId, string toEmail, string referrerName, string storeName, string acceptUrl)
    {
        InternetAddress address;
        try
        {
            address = InternetAddress.Parse(toEmail);
        }
        catch (Exception ex)
        {
            _logs.PayServer.LogWarning(ex, $"Invalid referrer email, invitation not sent: {toEmail}");
            return new EmailDispatchResult { IsSuccessful = false, FailedRecipients = { toEmail } };
        }

        var body = @$"Hi {referrerName},

You've been invited to the {storeName} referral program. Set your password to activate your account and start checking your referral credit balance:

{acceptUrl}

This link expires in 7 days.";

        var recipients = new List<EmailRecipient>
    {
        new()
        {
            Address = address,
            Subject = $"You're invited: {storeName} referral program",
            MessageText = body
        }
    };
        return await SendBulkEmail(storeId, recipients);
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

    private static string ApplyOrderFinancials(string body, Order order, string currency)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        if (order == null)
        {
            return body
                .Replace("{{Subtotal}}", string.Empty)
                .Replace("{{Discount}}", string.Empty)
                .Replace("{{DiscountCode}}", string.Empty)
                .Replace("{{DiscountLine}}", string.Empty)
                .Replace("{{Total}}", string.Empty);
        }

        var subtotal = order.SubtotalAmount ?? order.TotalAmount;
        var hasDiscount = order.DiscountAmount > 0;
        var discountLine = hasDiscount
            ? $"Discount ({order.DiscountCodeValue}): -{order.DiscountAmount:N2} {currency}"
            : string.Empty;

        return body
            .Replace("{{Subtotal}}", $"{subtotal:N2} {currency}")
            .Replace("{{Discount}}", hasDiscount ? $"-{order.DiscountAmount:N2} {currency}" : string.Empty)
            .Replace("{{DiscountCode}}", hasDiscount ? order.DiscountCodeValue : string.Empty)
            .Replace("{{DiscountLine}}", discountLine)
            .Replace("{{Total}}", $"{order.TotalAmount:N2} {currency}");
    }
}

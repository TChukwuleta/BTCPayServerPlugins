using System.Threading.Tasks;
using System;
using BTCPayServer.Logging;
using BTCPayServer.Services.Mails;
using MimeKit;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.SimpleTicketSales.Data;
using System.IO;
using System.Reflection;
using System.Linq;

namespace BTCPayServer.Plugins.SimpleTicketSales.Services;

public class EmailService
{
    private readonly EmailSenderFactory _emailSender;
    private readonly Logs _logs;
    public EmailService(EmailSenderFactory emailSender, Logs logs)
    {
        _logs = logs;
        _emailSender = emailSender;
    }

    private async Task<EmailDispatchResult> SendBulkEmail(string storeId, IEnumerable<EmailRecipient> recipients)
    {
        var settings = await (await _emailSender.GetEmailSender(storeId)).GetEmailSettings();
        if (!settings.IsComplete())
            return new EmailDispatchResult { IsSuccessful = false };

        var client = await settings.CreateSmtpClient();
        List<string> failedRecipients = new List<string>();
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
                    failedRecipients.Add(recipient.Address.ToString());
                    _logs.PayServer.LogError(ex, $"Error sending email to: {recipient.Address}");
                }
            }
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
        return new EmailDispatchResult { IsSuccessful = failedRecipients.Any(), FailedRecipients = failedRecipients };
    }

    public async Task<EmailDispatchResult> SendTicketRegistrationEmail(string storeId, ICollection<Ticket> tickets, Event ticketEvent)
    {
        var emailRecipients = new List<EmailRecipient>();
        foreach (var ticket in tickets)
        {
            string emailBody = ticketEvent.EmailBody
                            .Replace("{{Title}}", ticketEvent.Title)
                            .Replace("{{EventLink}}", ticketEvent.Location)
                            .Replace("{{Name}}", ticket.Name)
                            .Replace("{{Email}}", ticket.Email)
                            .Replace("{{Description}}", ticketEvent.Description)
                            .Replace("{{EventDate}}", ticketEvent.StartDate.ToString("MMMM dd, yyyy"))
                            .Replace("{{Amount}}", ticketEvent.Amount.ToString())
                            .Replace("{{Currency}}", ticketEvent.Currency);

            emailRecipients.Add(new EmailRecipient
            {
                Address = InternetAddress.Parse(ticket.Email),
                Subject = ticketEvent.EmailSubject,
                MessageText = ticketEvent.EmailBody
            });
        }
        return await SendBulkEmail(storeId, emailRecipients);
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

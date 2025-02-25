using System.Threading.Tasks;
using System;
using BTCPayServer.Logging;
using BTCPayServer.Services.Mails;
using MimeKit;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.SimpleTicketSales.Data;

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

    public async Task SendTicketRegistrationEmail(string storeId, TicketSalesEventTicket ticket, TicketSalesEvent ghostEvent)
    {
        string emailBody = ghostEvent.EmailBody
                            .Replace("{{Title}}", ghostEvent.Title)
                            .Replace("{{EventLink}}", ghostEvent.EventLink)
                            .Replace("{{Name}}", ticket.Name)
                            .Replace("{{Email}}", ticket.Email)
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

    public class EmailRecipient
    {
        public InternetAddress Address { get; set; }
        public string Subject { get; set; }
        public string MessageText { get; set; }
    }
}

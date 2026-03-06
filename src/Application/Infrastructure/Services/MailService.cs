using System.Net;
using System.Net.Mail;

using Application.Common.Interfaces;

using Ardalis.GuardClauses;

using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class MailService(IConfiguration configuration) : IMailService
{
    private readonly MailConfiguration? _mailSettings =
        configuration.GetSection("MailConfiguration").Get<MailConfiguration>();

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        Guard.Against.Null(_mailSettings, "Mail settings are not provided in the app settings file!");

        MailMessage message = new()
        {
            From = new MailAddress(_mailSettings.From),
            Subject = subject,
        };
        message.To.Add(new MailAddress(email));
        message.Body = "<html><body>" + htmlMessage + "</body></html>";
        message.IsBodyHtml = true;

        var smtpClient = new SmtpClient(_mailSettings.Host)
        {
            UseDefaultCredentials = false,
            Port = _mailSettings.Port,
            Credentials = new NetworkCredential(_mailSettings.Username, _mailSettings.Password),
        };

        if (!string.IsNullOrEmpty(_mailSettings.PickupDirectoryLocation))
        {
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtpClient.PickupDirectoryLocation = _mailSettings.PickupDirectoryLocation;
        }

        await smtpClient.SendMailAsync(message);
    }

    private class MailConfiguration
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string From { get; set; }
        public required string PickupDirectoryLocation { get; set; }
    }
}

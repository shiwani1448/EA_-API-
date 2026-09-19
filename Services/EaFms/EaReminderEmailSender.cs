using System.Net;
using System.Net.Mail;
using Jarvis5.Common;

namespace Jarvis5.Services.EaFms;

/// <summary>EA-specific immediate SMTP sender. It deliberately has no HR workflow/template dependency.</summary>
public sealed class EaReminderEmailSender : IEaReminderEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EaReminderEmailSender> _logger;

    public EaReminderEmailSender(IConfiguration configuration, ILogger<EaReminderEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(EaReminderEmailMessage message, CancellationToken ct = default)
    {
        var settings = _configuration.GetSection("EmailSettings");
        var host = settings["SmtpHost"];
        var username = settings["Username"];
        var password = settings["Password"];
        var fromEmail = settings["FromEmail"];
        var fromName = settings["FromName"] ?? "EA FMS";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromEmail))
            throw new BusinessRuleException("EA email delivery is not configured.");
        if (!int.TryParse(settings["SmtpPort"] ?? "587", out var port) || port is < 1 or > 65535)
            throw new BusinessRuleException("EA email delivery configuration is invalid.");

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };
            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false
            };
            mail.To.Add(message.To);
            ct.ThrowIfCancellationRequested();
            await client.SendMailAsync(mail);
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send EA reminder email to {Recipient}", message.To);
            throw new BusinessRuleException("Unable to send reminder email.");
        }
    }
}
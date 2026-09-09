using System.Net;
using System.Net.Mail;

namespace hrms_api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendRejectionEmailAsync(string toEmail, string candidateName, string positionTitle)
    {
        var settings = _config.GetSection("EmailSettings");
        var host = settings["SmtpHost"];
        var port = int.Parse(settings["SmtpPort"] ?? "587");
        var username = settings["Username"];
        var password = settings["Password"];
        var fromEmail = settings["FromEmail"] ?? "noreply@hrms.com";
        var fromName = settings["FromName"] ?? "HRMS HR Team";

        var subject = $"Update on your application for {positionTitle}";

        var body = $"""
            Dear {candidateName},

            Thank you for taking the time to apply for the {positionTitle} position at our organization
            and for your interest in joining our team.

            After careful consideration of your application, we regret to inform you that we will not
            be moving forward with your candidacy at this time. This decision was made after reviewing
            all applications against the requirements of the role.

            We appreciate the effort you put into your application and encourage you to apply for future
            openings that match your skills and experience. We will keep your profile on record for
            consideration in upcoming opportunities.

            We wish you the very best in your job search and future endeavors.

            Warm regards,
            {fromName}
            """;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send rejection email to {Email} for position {Position}", toEmail, positionTitle);
        }
    }

    public async Task SendInterviewInvitationEmailAsync(string toEmail, string candidateName, string positionTitle)
    {
        var settings = _config.GetSection("EmailSettings");
        var host = settings["SmtpHost"];
        var port = int.Parse(settings["SmtpPort"] ?? "587");
        var username = settings["Username"];
        var password = settings["Password"];
        var fromEmail = settings["FromEmail"] ?? "noreply@hrms.com";
        var fromName = settings["FromName"] ?? "HRMS HR Team";
        var companyName = settings["CompanyName"] ?? fromName;

        var subject = $"You have been shortlisted for the next round at {companyName}";

        var body = $"""
            Dear {candidateName},

            Thank you for taking the time to speak with us about the {positionTitle} position at {companyName}.

            We are pleased to inform you that, based on our conversation, you have been shortlisted to move
            forward to the next round of the interview process. Our team will be in touch shortly with the
            details and schedule for your upcoming interview.

            We appreciate your interest in joining our team and look forward to speaking with you again soon.

            Warm regards,
            {fromName}
            """;

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send interview invitation email to {Email} for position {Position}", toEmail, positionTitle);
        }
    }
}

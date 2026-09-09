namespace hrms_api.Services;

public interface IEmailService
{
    Task SendRejectionEmailAsync(string toEmail, string candidateName, string positionTitle);
    Task SendInterviewInvitationEmailAsync(string toEmail, string candidateName, string positionTitle);
}

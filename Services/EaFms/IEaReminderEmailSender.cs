namespace Jarvis5.Services.EaFms;

public sealed record EaReminderEmailMessage(string To, string Subject, string Body);

public interface IEaReminderEmailSender
{
    Task SendAsync(EaReminderEmailMessage message, CancellationToken ct = default);
}
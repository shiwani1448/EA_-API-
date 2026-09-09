namespace Jarvis5.Services;

public interface ICurrentUserService
{
    long UserId { get; }
    string? UserName { get; }
    string? IPAddress { get; }
    string? DeviceInfo { get; }
}

namespace Jarvis5.Services;

public interface ICurrentUserService
{
    long UserId { get; }
    string? EmployeeId => UserId > 0 ? UserId.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    string? UserName { get; }
    string? IPAddress { get; }
    string? DeviceInfo { get; }
}

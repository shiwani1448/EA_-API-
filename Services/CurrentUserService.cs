using Microsoft.AspNetCore.Http;

namespace Jarvis5.Services;

/// <summary>
/// Resolves the acting user from the shared HRMS JWT identity.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long UserId
    {
        get
        {
            var value = EmployeeId;
            return long.TryParse(value, out var id) ? id : 0;
        }
    }

    public string? EmployeeId => Claim("employeeID", "employee_id", "empId", "sub", System.Security.Claims.ClaimTypes.NameIdentifier);

    public string? UserName => Claim("employeeName", "employee_name", "name", System.Security.Claims.ClaimTypes.Name, "unique_name");

    private string? Claim(params string[] types)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null) return null;
        foreach (var type in types)
        {
            var value = principal.Identities.Where(i => i.IsAuthenticated).SelectMany(i => i.Claims)
                .FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && value != "0") return value;
        }
        return null;
    }

    public string? IPAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null) return null;

            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? DeviceInfo => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
}

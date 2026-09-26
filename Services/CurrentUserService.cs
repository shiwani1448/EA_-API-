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

    public string? EmployeeId => Claim("employeeID", "employee_id", "empId", "sub", System.Security.Claims.ClaimTypes.NameIdentifier)
        ?? EaHeader("X-Employee-Id");

    public string? UserName => Claim("employeeName", "employee_name", "name", System.Security.Claims.ClaimTypes.Name, "unique_name")
        ?? EaHeader("X-Employee-Name");

    /// <summary>
    /// EA FMS screens have no HRMS token; they send the logged-in EA's id/name as headers so her work is
    /// attributed to her. Used only for /api/ea requests and only when the token gives no identity.
    /// </summary>
    private string? EaHeader(string name)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null || !context.Request.Path.StartsWithSegments("/api/ea")) return null;
        var value = context.Request.Headers[name].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value == "0") return null;
        // Names may arrive URI-encoded (non-ASCII safe).
        try { return Uri.UnescapeDataString(value); } catch (UriFormatException) { return value; }
    }

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

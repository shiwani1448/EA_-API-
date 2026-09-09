namespace hrms_api.Models;

public class User
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

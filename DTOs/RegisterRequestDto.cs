using System.ComponentModel.DataAnnotations;

namespace hrms_api.DTOs;

public class RegisterRequestDto
{
    [Required]
    [MinLength(3)]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Department { get; set; } = string.Empty;
}

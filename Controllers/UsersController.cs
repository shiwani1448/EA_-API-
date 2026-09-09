using hrms_api.Data;
using hrms_api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<UserListItemDto>>>> GetAll([FromQuery] string? department)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(u => u.Department.ToLower() == department.Trim().ToLower());

        var users = await query
            .OrderBy(u => u.FirstName)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                EmployeeId = u.EmployeeId,
                FullName = (u.FirstName + " " + u.LastName).Trim(),
                Email = u.Email,
                Department = u.Department,
                Role = u.Role
            })
            .ToListAsync();

        return Ok(ApiResponse<List<UserListItemDto>>.Ok(users, "Users fetched successfully."));
    }
}

using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Every Followup now owns its own "Follow-up" EaTask. The real EaTaskService relies on
/// Postgres-only SQL (FOR SHARE, advisory locks), so InMemory tests use this stand-in, which
/// seeds the "Follow-up" BusinessModule and inserts a real EaTask row per call — the same
/// approach ApprovalAiServiceTests uses for Approval's own EaTask creation. Writes go through
/// a side context on the same InMemory store so they never flush entities a test has added
/// to its own context but not saved yet.
/// </summary>
internal static class FollowupTestSupport
{
    private static EaFmsDbContext Side(EaFmsDbContext db) =>
        new((DbContextOptions<EaFmsDbContext>)db.GetService<IDbContextOptions>());

    /// <summary>A request identity as the HRMS token would carry it (employee id + name).</summary>
    public static Jarvis5.Services.ICurrentUserService User(string? employeeId, string? name)
    {
        var mock = new Mock<Jarvis5.Services.ICurrentUserService>();
        mock.SetupGet(u => u.EmployeeId).Returns(employeeId);
        mock.SetupGet(u => u.UserName).Returns(name);
        mock.SetupGet(u => u.UserId).Returns(0);
        return mock.Object;
    }

    public static long EnsureFollowupModule(EaFmsDbContext db)
    {
        using var side = Side(db);
        var existing = side.BusinessModules.FirstOrDefault(m => m.Name == FollowupService.FollowupBusinessModuleName);
        if (existing is not null) return existing.Id;
        var module = new BusinessModule
        {
            Name = FollowupService.FollowupBusinessModuleName, IsActive = true, IsDeleted = false,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow,
        };
        side.BusinessModules.Add(module);
        side.SaveChanges();
        return module.Id;
    }

    public static IEaTaskService EaTasks(EaFmsDbContext db)
    {
        EnsureFollowupModule(db);
        var mock = new Mock<IEaTaskService>();
        mock.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                using var side = Side(db);
                var module = side.BusinessModules.First(m => m.Id == dto.ModuleId);
                var task = new EaTask
                {
                    BusinessModuleId = module.Id, ModuleName = module.Name,
                    BusinessRecordId = dto.BusinessRecordId, Task = dto.Task, Description = dto.Description,
                    ExecutionStatus = "NotStarted", IsActive = true,
                    CreatedBy = "tester", CreatedDate = DateTime.UtcNow,
                };
                side.Tasks.Add(task);
                side.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id, ModuleId = module.Id, ModuleName = module.Name,
                    BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
                    ExecutionStatus = task.ExecutionStatus, IsActive = true,
                    CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate,
                };
            });
        return mock.Object;
    }
}

from pathlib import Path
import re
def edit(n,f):
 p=Path(n); s=p.read_text(encoding='utf-8-sig'); t=f(s)
 if t!=s:p.write_text(t,encoding='utf-8')
for p in Path('Tests/EaFms/Travel').glob('*.cs'):
 s=p.read_text(encoding='utf-8-sig')
 # Remaining target-typed constructors have the review dependency on the next line.
 s=re.sub(r',\s*new TaskReviewService\([^\n]+?\)\);', ');',s)
 s=s.replace(', new Mock<ITaskReviewService>().Object','').replace(', Mock.Of<ITaskReviewService>()','')
 p.write_text(s,encoding='utf-8')
edit('Services/EaFms/TaskReviewService.cs',lambda s:s.replace('''        return task is null || task.IsDeleted
            ? throw new NotFoundException($"EaTask {eaTaskId} not found.")
            : task;''','''        if (task is null || task.IsDeleted)
            throw new NotFoundException($"EaTask {eaTaskId} not found.");
        // Scope is checked on writes only. Historical reviews remain readable.
        var supported = await _db.BusinessModules.AnyAsync(m => m.Id == task.BusinessModuleId
            && (m.Name == "Delegation" || m.Name == "EA Approval"), ct);
        if (!supported)
            throw new BusinessRuleException("Task Review is supported only for Delegation and Approval Management.");
        return task;'''))
for n in ['Services/EaFms/ITaskReviewService.cs','DTOs/EaFms/TaskReviewDtos.cs','Entities/EaFms/TaskReview.cs','Program.cs']:
 edit(n,lambda s:s.replace('Meeting/Delegation/Travel/Approval','Delegation/Approval').replace('Meeting,\n/// Delegation, Travel, Approval','Delegation and Approval').replace('Meeting,\n/// Delegation, Travel and Approval, since only EaTask is guaranteed to exist for all four.','Delegation and Approval. Historical cycles for other modules remain stored.'))
edit('Tests/EaFms/TaskReview/TaskReviewServiceTests.cs',lambda s:s.replace('ModuleName = "Meeting"','ModuleName = "Delegation", BusinessModuleId = 1').replace('''    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);''','''    private static EaFmsDbContext NewDb()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.BusinessModules.Add(new BusinessModule { Id = 1, Name = "Delegation", CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        db.SaveChanges();
        return db;
    }'''))
edit('Tests/EaFms/TaskReview/TaskReviewSwaggerTests.cs',lambda s:s.replace('new[] { "/api/ea/meetings/", "/api/ea/delegations/", "/api/ea/travel/requests/", "/api/ea/approvals/" }','new[] { "/api/ea/delegations/", "/api/ea/approvals/" }').replace('new[] { "MeetingDetailResponseDto", "MeetingListItemResponseDto", "MeetingLifecycleResponseDto", "DelegationResponseDto", "TravelRequestDetailDto", "TravelRequestListItemDto", "TravelRequestCreatedDto", "TravelActionResponseDto", "ApprovalListItemDto", "EaApprovalDetailDto" }','new[] { "DelegationResponseDto", "ApprovalListItemDto", "EaApprovalDetailDto" }').replace('''        Assert.DoesNotContain(paths, p => p.Key.StartsWith("/api/ea/task-reviews"));''','''        var reviewPaths = paths.Where(p => p.Key.EndsWith("/submit-for-review") || p.Key.EndsWith("/review/approve") || p.Key.EndsWith("/review/rework") || p.Key.EndsWith("/review/history")).ToList();
        Assert.Equal(8, reviewPaths.Count);
        Assert.DoesNotContain(reviewPaths, p => p.Key.StartsWith("/api/ea/meetings/") || p.Key.StartsWith("/api/ea/travel/"));
        Assert.DoesNotContain(paths, p => p.Key.StartsWith("/api/ea/task-reviews"));''').replace('''        Assert.Null(schemas["TaskReviewHistoryItemDto"]''','''        foreach (var name in new[] { "MeetingDetailResponseDto", "MeetingListItemResponseDto", "MeetingLifecycleResponseDto", "MeetingPauseResponseDto", "TravelRequestDetailDto", "TravelRequestListItemDto", "TravelRequestCreatedDto", "TravelActionResponseDto" })
        {
            Assert.NotNull(schemas[name]);
            Assert.Null(schemas[name]?["properties"]?["reviewSummary"]);
        }
        Assert.Null(schemas["TaskReviewHistoryItemDto"]'''))

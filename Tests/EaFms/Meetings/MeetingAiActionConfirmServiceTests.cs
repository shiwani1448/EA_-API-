using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

public class MeetingAiActionConfirmServiceTests
{
    [Fact]
    public async Task Confirm_UsesDelegationFlow_WithoutResolvingClaude()
    {
        await using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var meeting = new Meeting { CreatedBy = "test", CreatedDate = DateTime.UtcNow };
        db.Meetings.Add(meeting); await db.SaveChangesAsync();
        var provider = new Mock<IServiceProvider>(MockBehavior.Strict);
        provider.Setup(p => p.GetService(typeof(MeetingDelegationService)))
            .Returns(new MeetingDelegationService(db, null!, Mock.Of<IAuditService>()));
        var service = new MeetingAiService(db, provider.Object, Mock.Of<IMeetingActionExtractionPromptBuilder>(),
            Mock.Of<hrms_api.Services.IDocumentExtractionService>(), Mock.Of<IMeetingCompletionFileStore>(),
            NullLogger<MeetingAiService>.Instance, Options.Create(new ClaudeOptions()));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmActionsAsync(
            meeting.Id, new ConfirmMeetingAiActionsRequestDto(), "ea"));
        Assert.Equal("Complete the meeting before delegating tasks.", error.Message);
        provider.Verify(p => p.GetService(typeof(IClaudeClient)), Times.Never);
        Assert.Empty(db.MeetingActions);
    }
}

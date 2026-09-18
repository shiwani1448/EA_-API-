using Jarvis5.Services.EaFms;
using Xunit;

namespace Jarvis5.Tests.EaFms.EaTasks;

/// <summary>
/// EaTaskService.CreateCoreAsync always runs Postgres-only raw SQL (FOR SHARE row locks,
/// advisory locks) before its no-TAT guard is evaluated, so it cannot run against the
/// EF InMemory provider used elsewhere in this suite. This exercises the extracted
/// decision function directly — the exact function CreateCoreAsync consults — to verify
/// the no-TAT allow-list without a database.
/// </summary>
public class EaTaskNoTatAuthorizationTests
{
    [Theory]
    [InlineData("EA Approval")]
    [InlineData("Travel & Hospitality")]
    [InlineData("Delegation")]
    [InlineData(" EA Approval ")]
    [InlineData("ea approval")]
    [InlineData("delegation")]
    public void IsNoTatAuthorized_AuthorizedModules_ReturnsTrue(string moduleName)
    {
        Assert.True(EaTaskService.IsNoTatAuthorized(moduleName));
    }

    [Theory]
    [InlineData("Meeting")]
    [InlineData("Finance")]
    [InlineData("Some Other Module")]
    [InlineData("")]
    public void IsNoTatAuthorized_UnauthorizedModules_ReturnsFalse(string moduleName)
    {
        Assert.False(EaTaskService.IsNoTatAuthorized(moduleName));
    }
}

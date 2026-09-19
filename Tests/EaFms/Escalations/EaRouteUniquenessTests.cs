using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Escalations;

/// <summary>
/// Swagger/OpenAPI generation fails when two actions share one effective HTTP method + route.
/// These tests compute every effective route from the controllers' attributes and require them to be unique,
/// and check the Escalation create surface: exactly one public POST /api/ea/escalations.
/// </summary>
public class EaRouteUniquenessTests
{
    private static IEnumerable<(string Method, string Route, string Action)> Routes()
    {
        var controllers = typeof(EscalationsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttribute<ApiControllerAttribute>() != null);
        foreach (var c in controllers)
        {
            var prefix = c.GetCustomAttributes().OfType<IRouteTemplateProvider>().Select(r => r.Template).FirstOrDefault() ?? "";
            prefix = prefix.Replace("[controller]", c.Name.Replace("Controller", ""), StringComparison.OrdinalIgnoreCase);
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var http in m.GetCustomAttributes().OfType<HttpMethodAttribute>())
                {
                    var t = http.Template ?? "";
                    var route = t.StartsWith('/') || t.StartsWith("~/") ? t.TrimStart('~', '/')
                        : string.Join('/', new[] { prefix.Trim('/'), t.Trim('/') }.Where(x => x.Length > 0));
                    // {id:long} and {followupId:long} are the same shape for conflict purposes
                    var normalized = Regex.Replace(route.ToLowerInvariant(), @"\{[^}]+\}", "{}");
                    foreach (var verb in http.HttpMethods) yield return (verb, normalized, $"{c.Name}.{m.Name}");
                }
        }
    }

    [Fact]
    public void EveryEffectiveMethodAndRoute_IsUnique_AcrossAllControllers()
    {
        var duplicates = Routes().GroupBy(r => (r.Method, r.Route)).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Method} /{g.Key.Route}: {string.Join(", ", g.Select(x => x.Action))}").ToList();

        Assert.True(duplicates.Count == 0, "Duplicate effective routes: " + string.Join(" | ", duplicates));
    }

    [Fact]
    public void ExactlyOnePublicPostApiEaEscalations_OwnedByEscalationsController()
    {
        var posts = Routes().Where(r => r.Method == "POST" && r.Route == "api/ea/escalations").ToList();

        Assert.Equal("EscalationsController.Create", Assert.Single(posts).Action);
    }

    [Fact]
    public void FollowupsController_HasNoEscalationCreateAction_ButKeepsItsOwnOperations()
    {
        var actions = typeof(FollowupsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any()).Select(m => m.Name).ToHashSet();

        Assert.DoesNotContain("CreateEscalation", actions);
        Assert.DoesNotContain("CreateEscalationForFollowup", actions);
        foreach (var a in new[] { "GetList", "GetSummary", "Create", "GetById", "Update", "RecordFollowup", "Complete", "SendWhatsApp", "SendEmail", "GetEscalationsForFollowup" })
            Assert.Contains(a, actions);
        Assert.Contains(Routes(), r => r.Route == "api/ea/followups/{}/record-followup" && r.Method == "POST");
        Assert.Contains(Routes(), r => r.Route == "api/ea/followups/{}/cycles" && r.Method == "GET");
        Assert.Contains(Routes(), r => r.Route == "api/ea/followups/{}/cycles" && r.Method == "POST");
    }

    [Fact]
    public void EscalationReadAndActionRoutes_AreUnchanged()
    {
        var routes = Routes().Select(r => $"{r.Method} /{r.Route}").ToHashSet();

        foreach (var expected in new[]
        {
            "GET /api/ea/escalations", "GET /api/ea/escalations/{}", "POST /api/ea/escalations/{}/acknowledge",
            "POST /api/ea/escalations/{}/resolve", "GET /api/ea/escalation-levels"
        })
            Assert.Contains(expected, routes);
    }

    // ---- canonical endpoint still does the whole job (controller -> service -> EF) ----
    private static async Task<(EscalationsController C, EaFmsDbContext Db, long FollowupId)> ArrangeAsync()
    {
        var db = MakeDb();
        var f = new Followup { BusinessModuleId = 4, BusinessRecordId = "387", DueAt = Base, CreatedBy = "seed", CreatedDate = Base };
        db.Followups.Add(f);
        db.EscalationLevels.Add(new EscalationLevel { Id = 1, Code = "L1", Name = "L1", Level = 1, CreatedBy = "seed", CreatedDate = Base });
        db.EscalationLevels.Add(new EscalationLevel { Id = 2, Code = "L2", Name = "L2", Level = 2, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var svc = new EscalationService(new EscalationRepository(db), db, Mapper,
            Mock.Of<ICurrentUserService>(u => u.UserId == 0), Mock.Of<IAuditService>());
        return (new EscalationsController(svc), db, f.Id);
    }

    [Fact]
    public async Task CanonicalCreate_Returns201_PreservesFollowupAndInheritsSource()
    {
        var (c, db, followupId) = await ArrangeAsync(); await using var _ = db;

        var result = await c.Create(new CreateEscalationRequestDto { FollowupId = followupId, EscalationLevelId = 1, Notes = "n" }, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(EscalationsController.GetById), created.ActionName);
        var dto = Assert.IsType<EscalationResponseDto>(created.Value);
        Assert.Equal((followupId, 4L, "387"), (dto.FollowupId, dto.BusinessModuleId, dto.BusinessRecordId));
        Assert.Equal(1, await db.Escalations.CountAsync());
    }

    [Fact]
    public async Task CanonicalCreate_KeepsExistingValidation()
    {
        var (c, db, followupId) = await ArrangeAsync(); await using var _ = db;

        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => c.Create(new CreateEscalationRequestDto { FollowupId = 9999, EscalationLevelId = 1 }, default));
        await Assert.ThrowsAsync<Jarvis5.Common.NotFoundException>(() => c.Create(new CreateEscalationRequestDto { FollowupId = followupId, EscalationLevelId = 99 }, default));
        await Assert.ThrowsAsync<Jarvis5.Common.BadRequestException>(() => c.Create(new CreateEscalationRequestDto { FollowupId = followupId, EscalationLevelId = 2, NextEscalationLevelId = 1 }, default));
        Assert.Equal(0, await db.Escalations.CountAsync());
        Assert.False(new Jarvis5.Validators.CreateEscalationRequestDtoValidator().Validate(new CreateEscalationRequestDto()).IsValid);
    }

    [Fact]
    public async Task ListByFollowup_Register_Detail_Acknowledge_Resolve_StillWork()
    {
        var (c, db, followupId) = await ArrangeAsync(); await using var _ = db;
        var created = (EscalationResponseDto)((CreatedAtActionResult)await c.Create(new CreateEscalationRequestDto { FollowupId = followupId, EscalationLevelId = 1 }, default)).Value!;

        var byFollowup = Assert.IsType<OkObjectResult>(await c.List(new EscalationListQueryDto { FollowupId = followupId }, default));
        var register = Assert.IsType<OkObjectResult>(await c.List(new EscalationListQueryDto(), default));
        var detail = Assert.IsType<OkObjectResult>(await c.GetById(created.Id, default));
        Assert.IsType<NoContentResult>(await c.Acknowledge(created.Id, new AcknowledgeEscalationRequestDto { AcknowledgementNote = "seen" }, default));
        Assert.IsType<NoContentResult>(await c.Resolve(created.Id, new ResolveEscalationRequestDto { ResolutionNote = "done" }, default));
        var after = (EscalationResponseDto)((OkObjectResult)await c.GetById(created.Id, default)).Value!;

        Assert.Single((System.Collections.IEnumerable)byFollowup.Value!);
        Assert.NotNull(register.Value);
        Assert.Equal(created.Id, ((EscalationResponseDto)detail.Value!).Id);
        Assert.Equal("Resolved", after.EscalationState);
        Assert.IsType<BadRequestObjectResult>(await c.List(new EscalationListQueryDto { FollowupId = -1 }, default));
    }
}

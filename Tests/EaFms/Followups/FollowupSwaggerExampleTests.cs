using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Filters;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// The POST /api/ea/followups Swagger example must be neutral (no business-looking sample data), must
/// bind to the real DTO, and must not change what the backend stores.
/// </summary>
public class FollowupSwaggerExampleTests
{
    private static JsonObject BuildExample()
    {
        var schema = new OpenApiSchema { Properties = new System.Collections.Generic.Dictionary<string, IOpenApiSchema>() };
        foreach (var p in typeof(CreateFollowupRequestDto).GetProperties())
            schema.Properties[JsonNamingPolicy.CamelCase.ConvertName(p.Name)] = new OpenApiSchema();

        new EaCreateRequestSchemaFilter().Apply(schema, new SchemaFilterContext(typeof(CreateFollowupRequestDto), null!, null!));

        return Assert.IsType<JsonObject>(schema.Example);
    }

    [Fact]
    public void Example_ContainsNoBusinessLookingValues_OrFixedDates()
    {
        var json = BuildExample().ToJsonString();

        foreach (var forbidden in new[] { "Vendor", "External Vendor", "TEST-USER", "Test User", "2026-09", "Siddhi", "S5I-", "quotation", "General" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        Assert.All(BuildExample(), kv => Assert.True(kv.Value is null || kv.Value is JsonValue v && v.TryGetValue<bool>(out var b) && !b,
            $"{kv.Key} should be null or false"));
    }

    [Fact]
    public void Example_ExposesEveryRealRequestField_ExceptTheNonNullableDueAt()
    {
        var example = BuildExample();
        var real = typeof(CreateFollowupRequestDto).GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet();

        Assert.Equal(real.Where(n => n != "dueAt").OrderBy(x => x), example.Select(kv => kv.Key).OrderBy(x => x));
        foreach (var f in new[] { "employeeId", "employeeName", "reminderRecipientEmployeeId", "reminderRecipientName", "reminderRecipientEmail",
                     "reminderWhatsAppNumber", "waitingOnId", "waitingOnName", "waitingOnExternal", "reminderAt", "nextFollowupAt", "businessModuleId", "businessRecordId" })
            Assert.True(example.ContainsKey(f) && example[f] is null, f);
        Assert.False((bool)example["reminderSendEmail"]!);
        Assert.False((bool)example["reminderSendWhatsApp"]!);
    }

    [Fact]
    public async Task ExampleBody_BindsToTheRealDto_AndStoresNothingInvented()
    {
        await using var db = MakeDb();
        var svc = new FollowupService(new FollowupRepository(db), db, Mapper, Mock.Of<ICurrentUserService>(u => u.UserId == 0),
            Mock.Of<IAuditService>(), new FollowupSourceResolver(db));
        // Disallow-unmapped-members deserialization proves every example key is a real DTO property.
        var dto = JsonSerializer.Deserialize<CreateFollowupRequestDto>(BuildExample().ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var f = await svc.CreateAsync(dto);

        var row = await db.Followups.AsNoTracking().SingleAsync();
        Assert.Null(row.Subject); Assert.Null(row.Type); Assert.Null(row.Note);
        Assert.Null(row.DoerId); Assert.Null(row.DoerName);
        Assert.Null(row.WaitingOnId); Assert.Null(row.WaitingOnName); Assert.Null(row.WaitingOnExternal);
        Assert.Null(row.ResponseOwnerName); Assert.Null(row.ReminderAt); Assert.Null(row.NextFollowupAt);
        Assert.Null(row.ReminderRecipientName); Assert.Null(row.ReminderWhatsAppNumber);
        Assert.False(row.ReminderSendEmail || row.ReminderSendWhatsApp);
        Assert.Equal(1, row.DueAt.Year);          // no invented September 2026 date
        Assert.Equal("0", f.CreatedBy);
    }

    [Fact]
    public async Task RealValuesSentByTheFrontend_AreStillProcessedAndStored()
    {
        await using var db = MakeDb();
        var svc = new FollowupService(new FollowupRepository(db), db, Mapper, Mock.Of<ICurrentUserService>(u => u.UserId == 0),
            Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

        var f = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            Subject = "Real subject", Type = "Action", Remark = "Real remark", DueAt = new DateTime(2027, 1, 5, 9, 0, 0, DateTimeKind.Utc),
            WaitingOnName = "Acme", WaitingOnExternal = "Acme Ltd", DoerId = "E-7", DoerName = "Riya",
            EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav"
        });

        var row = await db.Followups.AsNoTracking().SingleAsync();
        Assert.Equal(("Real subject", "Action", "Real remark", "Acme", "Acme Ltd", "E-7", "Riya"),
            (row.Subject, row.Type, row.Note, row.WaitingOnName, row.WaitingOnExternal, row.DoerId, row.DoerName));
        Assert.Equal(new DateTime(2027, 1, 5, 9, 0, 0, DateTimeKind.Utc), row.DueAt);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (f.CreatedByEmployeeId, f.CreatedByEmployeeName));
    }

    [Fact]
    public void RuntimeDtoDefaults_ContainNoBusinessValues()
    {
        foreach (var t in new[] { typeof(CreateFollowupRequestDto), typeof(UpdateFollowupRequestDto), typeof(RecordFollowupRequestDto), typeof(CreateFollowupCycleRequestDto) })
        {
            var instance = Activator.CreateInstance(t)!;
            foreach (var p in t.GetProperties())
            {
                var v = p.GetValue(instance);
                Assert.True(v is null || v is bool b && !b || v is DateTime d && d == default || v is int i && i == 0,
                    $"{t.Name}.{p.Name} has a default value: {v}");
            }
        }
    }
}

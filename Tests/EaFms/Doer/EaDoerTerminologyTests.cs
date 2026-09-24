using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Doer;

// The old terminology is spelled out in these tests on purpose: they prove it is gone from the EA model and contracts.
// ReSharper disable StringLiteralTypo
public class EaDoerTerminologyTests
{
    private static string[] Names(Type t) => t.GetProperties().Select(p => p.Name).ToArray();

    private static readonly Type[] AllEaContractTypes = typeof(DelegationEntity).Assembly.GetTypes()
        .Where(t => t.Namespace is "Jarvis5.Dtos.EaFms" or "Jarvis5.Entities.EaFms").ToArray();

    /// <summary>The only allowed "Assignee" names: Delegation's explicit Assignee — a separate person
    /// from the Doer (requested for the Delegation API). "AssignedTo" stays banned everywhere, and
    /// no other entity/DTO may use "Assignee".</summary>
    private static readonly string[] ExplicitAssigneeResponseAliases =
    {
        "Delegation.AssigneeId", "Delegation.AssigneeNameSnapshot",
        "DelegationCreateRequestDto.AssigneeId", "DelegationCreateRequestDto.AssigneeName",
        "DelegationUpdateRequestDto.AssigneeId", "DelegationUpdateRequestDto.AssigneeName",
        "DelegationCreateCommand.AssigneeId", "DelegationCreateCommand.AssigneeNameSnapshot",
        "DelegationResponseDto.AssigneeId", "DelegationResponseDto.AssigneeName",
        // Meeting action items carry the future Delegation's assignee.
        "MeetingAction.AssigneeId", "MeetingAction.AssigneeName",
        "CreateMeetingActionDto.AssigneeId", "CreateMeetingActionDto.AssigneeName",
        "MeetingActionDto.AssigneeId", "MeetingActionDto.AssigneeName",
    };

    [Fact]
    public void NoEaEntityDtoOrCommand_StillCarriesAssignedToOrAssigneeTerminology()
    {
        var offenders = AllEaContractTypes.Concat(new[] { typeof(DelegationCreateCommand) })
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => n.Contains("AssignedTo", StringComparison.OrdinalIgnoreCase) || n.Contains("Assignee", StringComparison.OrdinalIgnoreCase))
            .Except(ExplicitAssigneeResponseAliases)
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData(typeof(DelegationCreateRequestDto), "DoerId", "DoerNameSnapshot")]
    [InlineData(typeof(DelegationUpdateRequestDto), "DoerId", "DoerNameSnapshot")]
    [InlineData(typeof(DelegationResponseDto), "DoerId", "DoerName")]
    [InlineData(typeof(DelegationListQueryDto), "DoerId", null)]
    [InlineData(typeof(DelegationCreateCommand), "DoerId", "DoerNameSnapshot")]
    [InlineData(typeof(DelegationEntity), "DoerId", "DoerNameSnapshot")]
    [InlineData(typeof(MeetingActionDto), "DoerId", "DoerName")]
    [InlineData(typeof(CreateMeetingActionDto), "DoerId", "DoerName")]
    [InlineData(typeof(MeetingAction), "DoerId", "DoerName")]
    [InlineData(typeof(CreateAssignmentRequestDto), "DoerId", "DoerName")]
    [InlineData(typeof(MeetingAssignmentSummaryDto), "DoerId", "DoerName")]
    [InlineData(typeof(MeetingAssignmentSummaryDto), "PreviousDoerId", "PreviousDoerName")]
    [InlineData(typeof(MeetingListItemResponseDto), "DoerId", "DoerName")]
    [InlineData(typeof(WorkAssignment), "DoerId", "DoerName")]
    [InlineData(typeof(WorkflowInstance), "DoerId", "DoerName")]
    [InlineData(typeof(CreateFollowupRequestDto), "DoerId", "DoerName")]
    [InlineData(typeof(UpdateFollowupRequestDto), "DoerId", "DoerName")]
    [InlineData(typeof(FollowupResponseDto), "DoerId", "DoerName")]
    [InlineData(typeof(FollowupListQueryDto), "DoerId", null)]
    [InlineData(typeof(Followup), "DoerId", "DoerName")]
    [InlineData(typeof(CreateIntakeRequestDto), "DoerId", null)]
    [InlineData(typeof(UpdateIntakeRequestDto), "DoerId", null)]
    [InlineData(typeof(IntakeRequestResponseDto), "DoerId", "DoerName")]
    [InlineData(typeof(IntakeRequest), "DoerId", "DoerName")]
    public void EveryDoerConcept_UsesDoerNames(Type type, string id, string? name)
    {
        Assert.Contains(id, Names(type));
        if (name is not null) Assert.Contains(name, Names(type));
    }

    [Fact]
    public void MeetingActionOwnerName_BecameDoerName_ButAgendaAndDecisionOwnersAreUntouched()
    {
        Assert.DoesNotContain("OwnerName", Names(typeof(MeetingActionDto)));
        Assert.DoesNotContain("OwnerName", Names(typeof(CreateMeetingActionDto)));
        Assert.DoesNotContain("OwnerName", Names(typeof(MeetingAction)));
        Assert.Contains("OwnerName", Names(typeof(MeetingAgenda)));      // agenda owner: a different concept
        Assert.Contains("OwnerName", Names(typeof(MeetingDecision)));    // decision owner: a different concept
    }

    [Fact]
    public void AssignedBy_Approver_ReminderRecipient_ResponseOwner_AndActorFields_AreUnchanged()
    {
        Assert.Contains("AssignedById", Names(typeof(DelegationEntity)));
        Assert.Contains("AssignedByNameSnapshot", Names(typeof(DelegationEntity)));
        Assert.Contains("AssignedById", Names(typeof(DelegationResponseDto)));
        Assert.Contains("AssignedByName", Names(typeof(DelegationResponseDto)));
        Assert.Contains("AssignedById", Names(typeof(WorkAssignment)));
        Assert.Contains("AssignedByName", Names(typeof(WorkAssignment)));
        Assert.Contains("AssignedById", Names(typeof(MeetingAssignmentSummaryDto)));
        Assert.Contains("AssignedByName", Names(typeof(MeetingAssignmentSummaryDto)));

        foreach (var n in new[] { "ApproverId", "ApproverName", "ApprovedBy", "RejectedBy" })
            Assert.Contains(n, Names(typeof(ApprovalRequest)));

        foreach (var n in new[] { "ReminderRecipientEmployeeId", "ReminderRecipientName", "ReminderRecipientEmail", "ReminderWhatsAppNumber", "ResponseOwnerId", "ResponseOwnerName" })
            Assert.Contains(n, Names(typeof(Followup)));

        Assert.Contains("CreatedBy", Names(typeof(DelegationEntity)));
        Assert.Contains("ModifiedBy", Names(typeof(DelegationEntity)));
    }

    [Fact]
    public void EaTask_GotNoDoerColumnsAdded()
    {
        Assert.DoesNotContain(Names(typeof(EaTask)), n => n.Contains("Doer") || n.Contains("AssignedTo"));
        Assert.DoesNotContain(Names(typeof(ApprovalRequest)), n => n.Contains("Doer") || n.Contains("AssignedTo"));
        Assert.DoesNotContain(Names(typeof(TravelRequest)), n => n.Contains("AssignedTo"));
    }

    [Fact]
    public void EfModel_UsesDoerColumnsAndIndexes_WithNoAssignedToLeftAndNoColumnNameCompatibilityMappings()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var expected = new (Type Entity, string[] Columns)[]
        {
            (typeof(DelegationEntity), new[] { "DoerId", "DoerNameSnapshot" }),
            (typeof(Followup), new[] { "DoerId", "DoerName" }),
            (typeof(IntakeRequest), new[] { "DoerId", "DoerName" }),
            (typeof(MeetingAction), new[] { "DoerId", "DoerName" }),
            (typeof(WorkAssignment), new[] { "DoerId", "DoerName" }),
            (typeof(WorkflowInstance), new[] { "DoerId", "DoerName" }),
        };
        foreach (var (entityType, columns) in expected)
        {
            var entity = db.Model.FindEntityType(entityType)!;
            foreach (var c in columns)
            {
                var property = entity.FindProperty(c)!;
                Assert.Equal(c, property.GetColumnName());   // property name == column name: no HasColumnName compatibility mapping
            }
            Assert.DoesNotContain(entity.GetProperties(), p => (p.GetColumnName() ?? "").Contains("AssignedTo"));
        }
        foreach (var indexed in new[] { typeof(DelegationEntity), typeof(WorkAssignment), typeof(WorkflowInstance) })
            Assert.Contains(db.Model.FindEntityType(indexed)!.GetIndexes(), i => i.Properties.Single().Name == "DoerId");
    }

    [Fact]
    public void NoUsersHrmsOrJwtDependency_WasIntroducedIntoTheAffectedServices()
    {
        foreach (var service in new[] { typeof(DelegationService), typeof(FollowupService), typeof(MeetingLifecycleService) })
        {
            var parameterTypes = service.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.FullName ?? "");
            Assert.DoesNotContain(parameterTypes, t => t.StartsWith("hrms_api", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Jwt", StringComparison.OrdinalIgnoreCase) || t.Contains("UserManager", StringComparison.OrdinalIgnoreCase));
        }
    }
}

/// <summary>
/// The rename migration against real PostgreSQL in a throwaway database: seed rows under the OLD column names at the
/// previous migration, migrate forward, and prove every value, index and constraint arrived under the Doer names.
/// </summary>
public sealed class DoerMigrationFixture : IAsyncLifetime
{
    private const string Server = "Host=localhost;Port=5432;Username=postgres;Password=123456";
    private readonly string _name = "scratch_doer_mig_" + Guid.NewGuid().ToString("N")[..12];
    public string Connection => $"{Server};Database={_name}";
    public const string Previous = "20260921092938_AddDelegationTypeAndStartDate";
    public EaFmsDbContext Db() => new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(Connection).Options);

    public async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<string?[]>> Query(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        var rows = new List<string?[]>();
        while (await r.ReadAsync()) rows.Add(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? null : r.GetValue(i).ToString()).ToArray());
        return rows;
    }

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection($"{Server};Database=postgres"))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        await using var db = Db();
        await db.GetService<IMigrator>().MigrateAsync(Previous);   // schema as it was before the rename
        // Foreign keys are irrelevant to a column rename; the seed rows only need the old column names.
        await Exec("""
            SET session_replication_role = replica;
            INSERT INTO public.ea_delegations ("ReferenceNo","EaTaskId","Title","AssignedToId","AssignedToNameSnapshot","AssignedById","Status","CreatedBy","CreatedDate","IsDeleted")
              VALUES ('D-1',1,'t1','emp-1','Doer One','delegator-1','Pending','seed',now(),false),
                     ('D-2',2,'t2','emp-2',NULL,'delegator-2','Pending','seed',now(),false);
            INSERT INTO public.ea_meeting_actions ("MeetingId","Title","AssignedToId","OwnerName","CreatedBy","CreatedDate","IsDeleted")
              VALUES (1,'a1','emp-9','Action Doer','seed',now(),false),(1,'a2',NULL,'Legacy Owner Only','seed',now(),false);
            INSERT INTO public.ea_work_assignments ("AssignedToId","AssignedToName","AssignedById","AssignedAt","IsCurrent","IsDeleted","CreatedBy","CreatedDate")
              VALUES ('emp-7','Assigned Person','assigner-1',now(),true,false,'seed',now());
            INSERT INTO public.ea_workflow_instances ("StatusId","StartedAt","AssignedToId","AssignedToName","IsActive","IsDeleted","CreatedBy","CreatedDate")
              VALUES (1,now(),'emp-5','Workflow Doer',true,false,'seed',now());
            """);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection($"{Server};Database=postgres");
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

public class DoerMigrationTests : IClassFixture<DoerMigrationFixture>
{
    private readonly DoerMigrationFixture _fx;
    public DoerMigrationTests(DoerMigrationFixture fx) => _fx = fx;

    [Fact]
    public async Task RenameMigration_PreservesEveryValue_RenamesIndexesAndConstraints_AndLeavesNoAssignedToColumn()
    {
        await using (var db = _fx.Db())
            await db.Database.MigrateAsync();   // applies RenameEaAssignedToToDoer

        Assert.Equal(new[] { new[] { "emp-1", "Doer One" }, new[] { "emp-2", null } },
            (await _fx.Query("""SELECT "DoerId","DoerNameSnapshot" FROM public.ea_delegations ORDER BY "Id" """)).Select(r => r.ToArray()));
        Assert.Equal(new[] { new[] { "emp-9", "Action Doer" }, new[] { null, "Legacy Owner Only" } },
            (await _fx.Query("""SELECT "DoerId","DoerName" FROM public.ea_meeting_actions ORDER BY "Id" """)).Select(r => r.ToArray()));
        Assert.Equal(new[] { new[] { "emp-7", "Assigned Person", "assigner-1" } },
            (await _fx.Query("""SELECT "DoerId","DoerName","AssignedById" FROM public.ea_work_assignments""")).Select(r => r.ToArray()));
        Assert.Equal(new[] { new[] { "emp-5", "Workflow Doer" } },
            (await _fx.Query("""SELECT "DoerId","DoerName" FROM public.ea_workflow_instances""")).Select(r => r.ToArray()));

        Assert.Empty(await _fx.Query("""
            SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'
              AND (column_name ILIKE '%assignedto%' OR (table_name = 'ea_meeting_actions' AND column_name = 'OwnerName'))
            """));
        var indexes = (await _fx.Query("""SELECT indexname FROM pg_indexes WHERE schemaname='public' AND (indexname ILIKE '%doerid%' OR indexname ILIKE '%assignedto%')""")).Select(r => r[0]).ToList();
        Assert.Equal(new[] { "IX_ea_delegations_DoerId", "IX_ea_work_assignments_DoerId", "IX_ea_workflow_instances_DoerId" }, indexes.OrderBy(x => x));
        Assert.DoesNotContain(await _fx.Query("""SELECT conname FROM pg_constraint WHERE conname ILIKE '%assignedto%'"""), _ => true);
    }
}

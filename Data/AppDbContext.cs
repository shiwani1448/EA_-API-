using Jarvis5.Common;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Data;

public class AppDbContext : DbContext
{
    public const string RequestNoSequence = "scih_request_no_seq";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SCIHRequest> Requests => Set<SCIHRequest>();
    public DbSet<SCIHRequestHistory> RequestHistories => Set<SCIHRequestHistory>();
    public DbSet<SCIHAnalysis> Analyses => Set<SCIHAnalysis>();
    public DbSet<SCIHSolutionDesign> SolutionDesigns => Set<SCIHSolutionDesign>();
    public DbSet<SCIHApproval> Approvals => Set<SCIHApproval>();
    public DbSet<SCIHTask> Tasks => Set<SCIHTask>();
    public DbSet<SCIHStageMaster> StageMasters => Set<SCIHStageMaster>();
    public DbSet<SCIHTaskHistory> TaskHistories => Set<SCIHTaskHistory>();
    public DbSet<SCIHAttachment> Attachments => Set<SCIHAttachment>();
    public DbSet<SCIHSnagList> SnagLists => Set<SCIHSnagList>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>(RequestNoSequence).StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<SCIHRequest>(entity =>
        {
            entity.ToTable("SCIH_Request");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RequestNo).HasColumnType("varchar(30)").IsRequired();
            entity.Property(e => e.Title).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.DepartmentId).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.RaisedBy).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.RaisedAt).HasColumnType("timestamp");
            entity.Property(e => e.Status).HasColumnType("varchar(30)").IsRequired();
            entity.Property(e => e.Priority).HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.OverallProgress).HasColumnType("decimal(5,2)");
            entity.Property(e => e.ExpectedBenefit).HasColumnType("text");
            entity.Property(e => e.OverallStartDate).HasColumnType("timestamp");
            entity.Property(e => e.OverallEndDate).HasColumnType("timestamp");
            entity.Property(e => e.PainPointsJson).HasColumnName("PainPointsJson").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.AttachmentsJson).HasColumnName("AttachmentsJson").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.MetaJson).HasColumnName("MetaJson").HasColumnType("jsonb").HasDefaultValue("{}");
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp");

            entity.HasIndex(e => e.RequestNo).IsUnique();
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.RaisedBy);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.RaisedAt);
            entity.HasIndex(e => e.ParentRequestId);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SCIHRequestHistory>(entity =>
        {
            entity.ToTable("SCIH_RequestHistory");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StageName).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.Action).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.PreviousValue).HasColumnType("jsonb");
            entity.Property(e => e.NewValue).HasColumnType("jsonb");
            entity.Property(e => e.Remarks).HasColumnType("text");
            entity.Property(e => e.ActionDate).HasColumnType("timestamp");
            entity.Property(e => e.IPAddress).HasColumnType("varchar(100)");
            entity.Property(e => e.DeviceInfo).HasColumnType("text");

            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => e.ActionDate);

            // No navigation property: history rows must stay readable for the
            // full audit trail even after their parent request is soft-deleted,
            // so this FK is intentionally not tied to SCIHRequest's soft-delete filter.
            entity.HasOne<SCIHRequest>()
                .WithMany()
                .HasForeignKey(e => e.RequestId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SCIHAnalysis>(entity =>
        {
            entity.ToTable("SCIH_Analysis");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AnalysisJson).HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
            entity.Property(e => e.GeneratedByModel).HasColumnType("varchar(100)");
            entity.Property(e => e.Status).HasColumnType("varchar(30)").HasDefaultValue(SCIHAnalysisStatus.Draft).IsRequired();
            entity.Property(e => e.Version).HasDefaultValue(1).IsRequired();
            entity.Property(e => e.ReworkCount).HasDefaultValue(0).IsRequired();
            entity.Property(e => e.ComparisonJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp");

            // Generate/Save (Stage 1->2) keep updating the single latest row in place,
            // so a request usually has exactly one row. The Approval-rework path is the
            // only thing that inserts a new row (never overwrites), which is why RequestId
            // alone can no longer be unique — (RequestId, Version) is the real key.
            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => new { e.RequestId, e.Version }).IsUnique();

            entity.HasOne<SCIHRequest>()
                .WithMany()
                .HasForeignKey(e => e.RequestId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SCIHAnalysis>()
                .WithMany()
                .HasForeignKey(e => e.PreviousAnalysisId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SCIHSolutionDesign>(entity =>
        {
            entity.ToTable("SCIH_SolutionDesign");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SolutionJson).HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
            entity.Property(e => e.AttachmentsJson).HasColumnName("AttachmentsJson").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.AIModel).HasColumnType("varchar(100)");
            entity.Property(e => e.PromptVersion).HasColumnType("varchar(20)");
            entity.Property(e => e.GeneratedAt).HasColumnType("timestamp");
            entity.Property(e => e.Status).HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp");

            // One solution design per request; edits update this row in place and
            // Version tracks the revision count (full before/after snapshots live in
            // SCIH_RequestHistory, same pattern as SCIH_Analysis).
            entity.HasIndex(e => e.RequestId).IsUnique();

            entity.HasOne<SCIHRequest>()
                .WithMany()
                .HasForeignKey(e => e.RequestId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SCIHApproval>(entity =>
        {
            entity.ToTable("SCIH_Approval");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Decision).HasColumnType("varchar(30)").IsRequired();
            entity.Property(e => e.Comments).HasColumnType("text");
            entity.Property(e => e.RejectionReason).HasColumnType("text");
            entity.Property(e => e.ImprovementAreasJson).HasColumnName("ImprovementAreasJson").HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(e => e.ApprovedDate).HasColumnType("timestamp");
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp");

            // Many rounds per request over the life of a request (one per
            // submit-for-approval); never updated once Approved/Rejected.
            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => new { e.RequestId, e.ApprovalRound }).IsUnique();

            entity.HasOne<SCIHRequest>()
                .WithMany()
                .HasForeignKey(e => e.RequestId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SCIHTask>(entity =>
        {
            entity.ToTable("SCIH_Task");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.RequestId).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.ApprovedId).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.SolutionId).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.Module).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.CurrentStage).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.CurrentStatus).HasColumnType("varchar(30)").HasDefaultValue(SCIHTaskStatus.Pending).IsRequired();
            entity.Property(e => e.StageDetails).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
            entity.Property(e => e.DoerLead).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.OverallStartDate).HasColumnType("varchar(50)").IsRequired();
            entity.Property(e => e.OverallEndDate).HasColumnType("varchar(50)").IsRequired();
            entity.Property(e => e.Priority).HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.CreationDate).HasColumnType("varchar(50)").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.UpdationDate).HasColumnType("varchar(50)");
            entity.Property(e => e.UpdatedBy).HasColumnType("varchar(100)");
            entity.Property(e => e.IsDelete).HasColumnType("varchar(10)").HasDefaultValue("false").IsRequired();
            entity.Property(e => e.IsDeletedBy).HasColumnType("varchar(100)");

            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => e.CurrentStatus);

            // No DB-level foreign key: RequestId/ApprovedId/SolutionId are plain
            // string snapshots per the Task table spec, not enforced relations.
            entity.HasQueryFilter(e => e.IsDelete != "true");
        });

        modelBuilder.Entity<SCIHStageMaster>(entity =>
        {
            entity.ToTable("SCIH_StageMaster");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StageName).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.ChecklistJson).HasColumnName("ChecklistJson").HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
            entity.Property(e => e.TatSmallHours).HasColumnType("decimal(6,2)").HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.TatMediumHours).HasColumnType("decimal(6,2)").HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.TatLargeHours).HasColumnType("decimal(6,2)").HasDefaultValue(0m).IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.CreatedDate).HasColumnType("timestamptz");
            entity.Property(e => e.ModifiedBy).HasColumnType("varchar(100)");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamptz");

            entity.HasIndex(e => e.StageName).IsUnique();

            entity.HasQueryFilter(e => !e.IsDeleted);

            var stageMasterSeedDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

            entity.HasData(
                new SCIHStageMaster { Id = 1, StageName = "Mind Mapping", DisplayOrder = 1, IsActive = true, ChecklistJson = "[\"Requirement Reviewed\",\"Flow Prepared\",\"Approved by Lead\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 2, StageName = "Backend", DisplayOrder = 2, IsActive = true, ChecklistJson = "[\"API Created\",\"Validation Completed\",\"Repository Added\",\"Service Added\",\"Unit Testing Completed\"]", TatSmallHours = 4, TatMediumHours = 8, TatLargeHours = 16, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 3, StageName = "Frontend", DisplayOrder = 3, IsActive = true, ChecklistJson = "[\"Screen Completed\",\"Responsive Completed\",\"API Integrated\",\"Validation Added\"]", TatSmallHours = 4, TatMediumHours = 8, TatLargeHours = 16, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 4, StageName = "AI", DisplayOrder = 4, IsActive = true, ChecklistJson = "[\"Model Selected\",\"Prompt Designed\",\"Model Integrated\",\"Output Validated\"]", TatSmallHours = 4, TatMediumHours = 8, TatLargeHours = 16, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 5, StageName = "Database", DisplayOrder = 5, IsActive = true, ChecklistJson = "[\"Schema Designed\",\"Tables Created\",\"Indexes Added\",\"Migration Verified\"]", TatSmallHours = 2, TatMediumHours = 5, TatLargeHours = 10, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 6, StageName = "API Integration", DisplayOrder = 6, IsActive = true, ChecklistJson = "[\"Endpoints Mapped\",\"Integration Completed\",\"Error Handling Verified\",\"Response Validated\"]", TatSmallHours = 2, TatMediumHours = 5, TatLargeHours = 10, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 7, StageName = "Backend Testing", DisplayOrder = 7, IsActive = true, IsTestingStage = true, ChecklistJson = "[\"Test Cases Prepared\",\"Unit Tests Passed\",\"Bug Fixes Verified\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 8, StageName = "Frontend Testing", DisplayOrder = 8, IsActive = true, IsTestingStage = true, ChecklistJson = "[\"Test Cases Prepared\",\"UI Tests Passed\",\"Cross Browser Verified\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 9, StageName = "AI Testing", DisplayOrder = 9, IsActive = true, IsTestingStage = true, ChecklistJson = "[\"Test Dataset Prepared\",\"Accuracy Verified\",\"Edge Cases Tested\"]", TatSmallHours = 2, TatMediumHours = 5, TatLargeHours = 10, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 10, StageName = "Deployment", DisplayOrder = 10, IsActive = true, ChecklistJson = "[\"Build Verified\",\"Deployment Script Ready\",\"Deployed to Server\",\"Smoke Test Passed\"]", TatSmallHours = 1, TatMediumHours = 3, TatLargeHours = 6, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 11, StageName = "Documentation", DisplayOrder = 11, IsActive = true, ChecklistJson = "[\"User Guide Prepared\",\"Technical Doc Updated\",\"Reviewed by Lead\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 12, StageName = "Video", DisplayOrder = 12, IsActive = true, ChecklistJson = "[\"Script Prepared\",\"Recording Completed\",\"Editing Completed\"]", TatSmallHours = 3, TatMediumHours = 6, TatLargeHours = 12, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 13, StageName = "Video Testing", DisplayOrder = 13, IsActive = true, IsTestingStage = true, ChecklistJson = "[\"Video Reviewed\",\"Audio Verified\",\"Approved by Lead\"]", TatSmallHours = 1, TatMediumHours = 2, TatLargeHours = 4, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 14, StageName = "Training", DisplayOrder = 14, IsActive = true, ChecklistJson = "[\"Training Material Prepared\",\"Session Conducted\",\"Feedback Collected\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 15, StageName = "Handover", DisplayOrder = 15, IsActive = true, ChecklistJson = "[\"Documents Handed Over\",\"Access Provided\",\"Client Sign-off Received\"]", TatSmallHours = 1, TatMediumHours = 3, TatLargeHours = 6, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },

                // Snag List (RRR) module stages — Stage 6 (Testing & RRR).
                new SCIHStageMaster { Id = 16, StageName = "Snag Fix", DisplayOrder = 16, IsActive = true, ChecklistJson = "[\"Issue Analysed\",\"Code Fixed\",\"Unit Tested\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 17, StageName = "RRR", DisplayOrder = 17, IsActive = true, ChecklistJson = "[\"Root Cause Identified\",\"Resolution Documented\",\"Reviewed by Lead\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 18, StageName = "Merge & Make Live", DisplayOrder = 18, IsActive = true, ChecklistJson = "[\"Code Merged\",\"Deployment Completed\",\"Smoke Testing Completed\",\"Rollback Plan Ready\"]", TatSmallHours = 1, TatMediumHours = 3, TatLargeHours = 6, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 19, StageName = "Testing", DisplayOrder = 19, IsActive = true, IsTestingStage = true, ChecklistJson = "[\"Test Cases Executed\",\"Defect Fix Verified\",\"Regression Passed\"]", TatSmallHours = 3, TatMediumHours = 6, TatLargeHours = 12, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 20, StageName = "Final Feedback", DisplayOrder = 20, IsActive = true, ChecklistJson = "[\"Business User Reviewed\",\"Feedback Documented\",\"Sign-off Received\"]", TatSmallHours = 1, TatMediumHours = 2, TatLargeHours = 4, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 21, StageName = "Technical Documentation", DisplayOrder = 21, IsActive = true, ChecklistJson = "[\"Technical Doc Updated\",\"Developer Video Recorded\",\"Reviewed by Lead\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 22, StageName = "AI Documentation Review", DisplayOrder = 22, IsActive = true, ChecklistJson = "[\"Documentation Completeness Checked\",\"AI Quality Review Passed\",\"Flagged Issues Resolved\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate },
                new SCIHStageMaster { Id = 23, StageName = "User Training Documentation", DisplayOrder = 23, IsActive = true, ChecklistJson = "[\"User Guide Prepared\",\"Training Video Recorded\",\"Reviewed by Lead\"]", TatSmallHours = 2, TatMediumHours = 4, TatLargeHours = 8, IsDeleted = false, CreatedBy = "System", CreatedDate = stageMasterSeedDate }
            );
        });

        modelBuilder.Entity<SCIHTaskHistory>(entity =>
        {
            entity.ToTable("SCIH_TaskHistory");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StageName).HasColumnType("varchar(100)");
            entity.Property(e => e.Action).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.PreviousValue).HasColumnType("jsonb");
            entity.Property(e => e.NewValue).HasColumnType("jsonb");
            entity.Property(e => e.Remarks).HasColumnType("text");
            entity.Property(e => e.ActionDate).HasColumnType("timestamptz");
            entity.Property(e => e.IPAddress).HasColumnType("varchar(100)");
            entity.Property(e => e.DeviceInfo).HasColumnType("text");

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => e.ActionDate);

            // No DB-level foreign key to SCIH_Task: SCIH_Task.Id is now `int` per the
            // Task table spec, while TaskId here stays `long` (TaskHistory wasn't part
            // of that spec) — an EF relationship needs matching CLR types, so this is
            // an unenforced reference like SCIH_Task's own RequestId/ApprovedId/SolutionId.
            // History rows also need to stay readable after their task is soft-deleted.
        });

        modelBuilder.Entity<SCIHSnagList>(entity =>
        {
            entity.ToTable("SCIH_SnagList");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.RequestId).HasColumnType("varchar(100)");
            entity.Property(e => e.TaskId).HasColumnType("varchar(100)");
            entity.Property(e => e.Module).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.SnagDescription).HasColumnType("text").IsRequired();
            entity.Property(e => e.Priority).HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.CurrentStatus).HasColumnType("varchar(30)").HasDefaultValue(SCIHSnagStatus.Open).IsRequired();
            entity.Property(e => e.StageDetails).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
            entity.Property(e => e.CreationDate).HasColumnType("varchar(50)").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.UpdationDate).HasColumnType("varchar(50)");
            entity.Property(e => e.UpdatedBy).HasColumnType("varchar(100)");
            entity.Property(e => e.IsDelete).HasColumnType("varchar(10)").HasDefaultValue("false").IsRequired();
            entity.Property(e => e.IsDeletedBy).HasColumnType("varchar(100)");

            entity.HasIndex(e => e.RequestId);
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.CurrentStatus);

            // No DB-level foreign key: RequestId/TaskId are plain string snapshots
            // per the same convention as SCIH_Task, not enforced relations.
            entity.HasQueryFilter(e => e.IsDelete != "true");
        });

        modelBuilder.Entity<SCIHAttachment>(entity =>
        {
            entity.ToTable("SCIH_Attachments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EntityType).HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.FileName).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.OriginalName).HasColumnType("varchar(300)").IsRequired();
            entity.Property(e => e.FileUrl).HasColumnType("varchar(500)").IsRequired();
            entity.Property(e => e.ContentType).HasColumnType("varchar(100)");
            entity.Property(e => e.UploadedAt).HasColumnType("timestamp");
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp");
            entity.Property(e => e.UpdatedDate).HasColumnType("timestamp");

            // Deliberately no DB-level foreign key: EntityId can point at SCIH_Request,
            // SCIH_Task, or any future module's table, so it stays a plain value like
            // SCIH_Task's own RequestId/ApprovedId/SolutionId snapshots.
            entity.HasIndex(e => new { e.EntityType, e.EntityId });

            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}

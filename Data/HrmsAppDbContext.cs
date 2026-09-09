using hrms_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace hrms_api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WeatherForecast> WeatherForecasts => Set<WeatherForecast>();
    public DbSet<User> Users => Set<User>();
    public DbSet<HiringRequest> HiringRequests => Set<HiringRequest>();
    public DbSet<JDMaster> JDMasters => Set<JDMaster>();
    public DbSet<Sourcing> Sourcings => Set<Sourcing>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateBGVDocument> CandidateBGVDocuments => Set<CandidateBGVDocument>();
    public DbSet<CandidateActivity> CandidateActivities => Set<CandidateActivity>();
    public DbSet<CallRecording> CallRecordings => Set<CallRecording>();
    public DbSet<CandidateAIScreening> CandidateAIScreenings => Set<CandidateAIScreening>();
    public DbSet<ScreeningBatch> ScreeningBatches => Set<ScreeningBatch>();
    public DbSet<HrAiInterviewQuestion> HrAiInterviewQuestions => Set<HrAiInterviewQuestion>();
    public DbSet<DirectorAiInterviewQuestion> DirectorAiInterviewQuestions => Set<DirectorAiInterviewQuestion>();
    public DbSet<AssessmentQuestionBank> AssessmentQuestionBanks => Set<AssessmentQuestionBank>();
    public DbSet<AssessmentEvaluation> AssessmentEvaluations => Set<AssessmentEvaluation>();
    public DbSet<OnboardingAssessmentQuestionTestBank> OnboardingAssessmentQuestionTestBanks => Set<OnboardingAssessmentQuestionTestBank>();
    public DbSet<OnboardingAssessment> OnboardingAssessments => Set<OnboardingAssessment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.EmployeeId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<HiringRequest>(entity =>
        {
            entity.HasKey(h => h.RequestId);
            entity.Property(h => h.RequestId).ValueGeneratedOnAdd();
            entity.Property(h => h.BudgetMinLpa).HasColumnType("numeric(10,2)");
            entity.Property(h => h.BudgetMaxLpa).HasColumnType("numeric(10,2)");
        });

        modelBuilder.Entity<JDMaster>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Id).ValueGeneratedOnAdd();
            entity.HasIndex(j => new { j.Department, j.Designation });
        });

        modelBuilder.Entity<Sourcing>(entity =>
        {
            entity.HasKey(s => s.SourcingId);
            entity.Property(s => s.SourcingId).ValueGeneratedOnAdd();
            entity.Property(s => s.TaskDetails).HasColumnType("jsonb");
            entity.HasIndex(s => s.HiringRequestId);
        });

        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasKey(c => c.CandidateId);
            entity.Property(c => c.CandidateId).ValueGeneratedOnAdd();
            entity.HasIndex(c => c.RequisitionId);
            entity.HasIndex(c => c.Email);
            entity.Property(c => c.CurrentCtcLpa).HasColumnType("numeric(10,2)");
            entity.Property(c => c.ExpectedCtcLpa).HasColumnType("numeric(10,2)");
            entity.Property(c => c.CurrentStage).HasDefaultValue("applied");
            entity.Property(c => c.CurrentStatus).HasDefaultValue("pending");
        });

        modelBuilder.Entity<CandidateBGVDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).ValueGeneratedOnAdd();
            entity.HasIndex(d => d.CandidateId);
            entity.HasIndex(d => new { d.CandidateId, d.CheckType, d.DocumentType });
            entity.HasOne(d => d.Candidate)
                .WithMany()
                .HasForeignKey(d => d.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });



        modelBuilder.Entity<CandidateAIScreening>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.HasIndex(s => s.CandidateId);
            entity.HasIndex(s => new { s.CandidateId, s.Status });
            entity.Property(s => s.OverallScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.ConfidenceScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.RoleFitScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.SkillFitScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.ExperienceRelevanceScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.AchievementImpactScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.CareerStabilityScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.EducationCertificationScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.IndustryAlignmentScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.GrowthPotentialScore).HasColumnType("numeric(5,2)");
            entity.Property(s => s.RedFlagDeduction).HasColumnType("numeric(5,2)");
        });

        modelBuilder.Entity<ScreeningBatch>(entity =>
        {
            entity.HasKey(b => b.BatchId);
            entity.HasIndex(b => b.Status);
            entity.HasIndex(b => b.CreatedAt);
        });

        modelBuilder.Entity<HrAiInterviewQuestion>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Id).ValueGeneratedOnAdd();
            entity.HasIndex(q => q.CandidateId);
            entity.HasIndex(q => q.InterviewRoundId);
            entity.HasIndex(q => new { q.CandidateId, q.InterviewRoundId, q.QuestionNo }).IsUnique();
            entity.Property(q => q.DScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.IScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.SScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.CScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<DirectorAiInterviewQuestion>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Id).ValueGeneratedOnAdd();
            entity.HasIndex(q => q.CandidateId);
            entity.HasIndex(q => q.InterviewRoundId);
            entity.HasIndex(q => new { q.CandidateId, q.InterviewRoundId, q.QuestionNo }).IsUnique();
            entity.Property(q => q.ScreeningScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.DScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.IScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.SScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.CScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.HrRoundScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.TechnicalAssessmentScore).HasColumnType("numeric(5,2)");
            entity.Property(q => q.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<AssessmentQuestionBank>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Id).ValueGeneratedOnAdd();
            entity.HasIndex(q => new { q.Department, q.Designation, q.Level, q.RoundName, q.QuestionNo }).IsUnique();
            entity.HasIndex(q => new { q.Department, q.Designation, q.Level, q.RoundName, q.IsActive });
            entity.Property(q => q.Weightage).HasColumnType("numeric(5,2)");
            entity.Property(q => q.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<AssessmentEvaluation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.CandidateId);
            entity.HasIndex(e => new { e.CandidateId, e.InterviewId, e.RoundId, e.CreatedAt });
            entity.Property(e => e.TotalMarks).HasColumnType("numeric(8,2)");
            entity.Property(e => e.ObtainedMarks).HasColumnType("numeric(8,2)");
            entity.Property(e => e.Percentage).HasColumnType("numeric(5,2)");
            entity.Property(e => e.EvaluationJson).HasColumnType("jsonb");
            entity.HasOne(e => e.Candidate)
                .WithMany()
                .HasForeignKey(e => e.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OnboardingAssessmentQuestionTestBank>(entity =>
        {
            entity.HasKey(q => q.QuestionId);
            entity.Property(q => q.QuestionId).ValueGeneratedOnAdd();
            entity.HasIndex(q => new { q.Category, q.IsActive });
            entity.Property(q => q.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<OnboardingAssessment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedOnAdd();
            entity.Property(a => a.Status).IsRequired();
            entity.Property(a => a.PlanJson).HasColumnType("jsonb");
            entity.Property(a => a.ResponsesJson).HasColumnType("jsonb");
            entity.Property(a => a.FinalEvaluationJson).HasColumnType("jsonb");
            entity.HasIndex(a => a.CandidateId);
            entity.HasIndex(a => a.CandidateId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('InProgress', 'ReadyForFinalEvaluation', 'Evaluated', 'Held')");
            entity.HasOne(a => a.Candidate).WithMany().HasForeignKey(a => a.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CallRecording>(entity =>
        {
            entity.HasKey(r => r.RecordingId);
            entity.Property(r => r.RecordingId).ValueGeneratedOnAdd();
            entity.HasIndex(r => r.CandidateId);
            entity.HasIndex(r => r.RecordedAt);
        });

        // Npgsql rejects DateTime with Kind=Unspecified for "timestamp with time zone" columns.
        // Force every DateTime/DateTime? property to Kind=Utc on the way in and out.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => EnsureUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => EnsureUtc(v),
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtcConverter);
            }
        }
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime? EnsureUtc(DateTime? value) => value.HasValue ? EnsureUtc(value.Value) : value;
}

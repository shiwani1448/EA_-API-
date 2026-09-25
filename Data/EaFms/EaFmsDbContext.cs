using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Data.EaFms;

public class EaFmsDbContext : DbContext
{
    public EaFmsDbContext(DbContextOptions<EaFmsDbContext> options)
        : base(options)
    {
    }

    // EA FMS DbSets (foundation catalogs)
    public DbSet<BusinessModule> BusinessModules { get; set; } = null!;
    public DbSet<Status> Statuses { get; set; } = null!;
    public DbSet<PriorityLevel> PriorityLevels { get; set; } = null!;
    public DbSet<IntakeRequest> IntakeRequests { get; set; } = null!;
    public DbSet<IntakeClassification> IntakeClassifications { get; set; } = null!;
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; } = null!;
    public DbSet<WorkflowHistory> WorkflowHistory { get; set; } = null!;
    public DbSet<FollowupCycle> FollowupCycles { get; set; } = null!;
    public DbSet<Followup> Followups { get; set; } = null!;
    public DbSet<FollowupPhaseTat> FollowupPhaseTats { get; set; } = null!;
    public DbSet<FollowupReminderLog> FollowupReminderLogs { get; set; } = null!;
    public DbSet<Meeting> Meetings { get; set; } = null!;
    public DbSet<Escalation> Escalations { get; set; } = null!;
    public DbSet<EscalationLevel> EscalationLevels { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;
    // AI task-specific tables (one per real AI task, not a generic shared shape) are
    // declared just above each one's ToTable(...) mapping below.
    public DbSet<MeetingActionExtraction> MeetingActionExtractions { get; set; } = null!;
    public DbSet<TravelOptionSuggestion> TravelOptionSuggestions { get; set; } = null!;
    public DbSet<TravelOptionComparison> TravelOptionComparisons { get; set; } = null!;
    public DbSet<TravelItineraryDraft> TravelItineraryDrafts { get; set; } = null!;
    public DbSet<TravelChecklistDraft> TravelChecklistDrafts { get; set; } = null!;
    public DbSet<ApprovalReadinessCheck> ApprovalReadinessChecks { get; set; } = null!;
    public DbSet<ApprovalApproverRecommendation> ApprovalApproverRecommendations { get; set; } = null!;
    public DbSet<ApprovalStatusSummary> ApprovalStatusSummaries { get; set; } = null!;
    public DbSet<DelegationOwnerSuggestion> DelegationOwnerSuggestions { get; set; } = null!;
    public DbSet<DelegationDueDatePrediction> DelegationDueDatePredictions { get; set; } = null!;
    public DbSet<DelegationDelayRiskCheck> DelegationDelayRiskChecks { get; set; } = null!;
    public DbSet<CalendarQuickAddSuggestion> CalendarQuickAddSuggestions { get; set; } = null!;
    public DbSet<CalendarConflictCheck> CalendarConflictChecks { get; set; } = null!;
    public DbSet<FollowupReminderSuggestion> FollowupReminderSuggestions { get; set; } = null!;
    public DbSet<FollowupEscalationSuggestion> FollowupEscalationSuggestions { get; set; } = null!;
    public DbSet<FollowupResolutionPrediction> FollowupResolutionPredictions { get; set; } = null!;
    public DbSet<FollowupAtRiskCheck> FollowupAtRiskChecks { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<WorkPause> WorkPauses { get; set; } = null!;
    public DbSet<WorkAssignment> WorkAssignments { get; set; } = null!;
    public DbSet<WorkRevision> WorkRevisions { get; set; } = null!;
    public DbSet<EaTask> Tasks { get; set; } = null!;
    public DbSet<TaskReview> TaskReviews { get; set; } = null!;
    public DbSet<MeetingAgenda> MeetingAgendas { get; set; } = null!;
    public DbSet<MeetingAttendee> MeetingAttendees { get; set; } = null!;
    public DbSet<MeetingMinutes> MeetingMinutes { get; set; } = null!;
    public DbSet<MeetingDecision> MeetingDecisions { get; set; } = null!;
    public DbSet<MeetingAction> MeetingActions { get; set; } = null!;
    public DbSet<TatRule> TatRules { get; set; } = null!;

    // Approval management (EA-specific)
    public DbSet<Entities.EaFms.ApprovalRequest> ApprovalRequests { get; set; } = null!;
    public DbSet<Entities.EaFms.ApprovalCycle> ApprovalCycles { get; set; } = null!;
    // Per-phase TAT records (Actual / Review N / Rework N) — see ApprovalPhaseTat's own doc comment.
    public DbSet<Entities.EaFms.ApprovalPhaseTat> ApprovalPhaseTats { get; set; } = null!;
    public DbSet<TravelLocalTransport> TravelLocalTransports { get; set; } = null!;
    public DbSet<TravelHospitality> TravelHospitalityArrangements { get; set; } = null!;
    public DbSet<TravelExpense> TravelExpenses { get; set; } = null!;
    public DbSet<TravelBooking> TravelBookings { get; set; } = null!;
    public DbSet<TravelRequest> TravelRequests { get; set; } = null!;
    public DbSet<TravelRequestCycle> TravelRequestCycles { get; set; } = null!;
    public DbSet<TravelTraveller> TravelTravellers { get; set; } = null!;

    // Delegation (EA-specific) — Step 1 foundation
    public DbSet<Delegation> Delegations { get; set; } = null!;
    // Per-phase TAT records (Actual / Review N / Rework N) — see DelegationPhaseTat's own doc comment.
    public DbSet<DelegationPhaseTat> DelegationPhaseTats { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(Jarvis5.Common.EaFms.TatClassification)
            .GetMethod(nameof(Jarvis5.Common.EaFms.TatClassification.TrimForMatch))!)
            .HasName("btrim").IsBuiltIn();

        base.OnModelCreating(modelBuilder);

        // ============================================================
        // EA FMS DATABASE SCHEMA
        // ============================================================
        // EA FMS tables follow the existing project pattern and use the
        // database default schema (public). Do NOT use a separate schema.

        // ============================================================
        // UTC DATETIME HANDLING
        // ============================================================
        // PostgreSQL timestamptz requires UTC DateTime values.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => EnsureUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? EnsureUtc(v.Value) : v,
            v => v.HasValue
                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }

        // ============================================================
        // EA FMS ENTITY CONFIGURATION
        // ============================================================

        modelBuilder.Entity<BusinessModule>(entity =>
        {
            entity.Property(e => e.CreatedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.CreatedByEmployeeName).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeName).HasMaxLength(100);
            entity.ToTable("ea_business_modules", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
        });

        // Approval management sequence for ReferenceNo generation (EA-specific)
        modelBuilder.HasSequence<long>("ea_approval_no_seq").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("ea_travel_no_seq").StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>("ea_delegation_no_seq").StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<TravelLocalTransport>(entity =>
        {
            entity.ToTable("ea_travel_local_transports", "public", t =>
            {
                t.HasCheckConstraint("CK_ea_travel_local_transports_EstimatedCost", "\"EstimatedCost\" IS NULL OR \"EstimatedCost\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_local_transports_ActualCost", "\"ActualCost\" IS NULL OR \"ActualCost\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.TravelRequest).WithMany().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.TransportStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TransportType).HasMaxLength(50);
            entity.Property(x => x.PickupLocation).HasMaxLength(500);
            entity.Property(x => x.DropLocation).HasMaxLength(500);
            entity.Property(x => x.VehiclePreference).HasMaxLength(200);
            entity.Property(x => x.BookingReference).HasMaxLength(200);
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(10);
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 2);
            entity.Property(x => x.ActualCost).HasPrecision(18, 2);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModifiedBy).HasMaxLength(100);
            entity.HasIndex(x => x.TravelRequestId);
        });

        modelBuilder.Entity<TravelHospitality>(entity =>
        {
            entity.ToTable("ea_travel_hospitality_arrangements", "public", t =>
            {
                t.HasCheckConstraint("CK_ea_travel_hospitality_arrangements_EstimatedCost", "\"EstimatedCost\" IS NULL OR \"EstimatedCost\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_hospitality_arrangements_ActualCost", "\"ActualCost\" IS NULL OR \"ActualCost\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_hospitality_arrangements_NumberOfGuests", "\"NumberOfGuests\" IS NULL OR \"NumberOfGuests\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.TravelRequest).WithMany().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(500);
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.Currency).HasMaxLength(10);
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 2);
            entity.Property(x => x.ActualCost).HasPrecision(18, 2);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModifiedBy).HasMaxLength(100);
            entity.HasIndex(x => x.TravelRequestId);
        });

        modelBuilder.Entity<TravelExpense>(entity =>
        {
            entity.ToTable("ea_travel_expenses", "public", t =>
                t.HasCheckConstraint("CK_ea_travel_expenses_Amount", "\"Amount\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.TravelRequest).WithMany().HasForeignKey(x => x.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReceiptAttachment).WithMany().HasForeignKey(x => x.ReceiptAttachmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(10);
            entity.Property(x => x.SubmittedBy).HasMaxLength(100);
            entity.Property(x => x.ApprovedBy).HasMaxLength(100);
            entity.Property(x => x.RejectedBy).HasMaxLength(100);
            entity.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModifiedBy).HasMaxLength(100);
            entity.HasIndex(x => x.TravelRequestId);
            entity.HasIndex(x => x.ReceiptAttachmentId);
        });

        modelBuilder.Entity<TravelBooking>(entity =>
        {
            entity.ToTable("ea_travel_bookings", "public", table =>
                table.HasCheckConstraint("CK_ea_travel_bookings_Cost", "\"Cost\" IS NULL OR \"Cost\" >= 0"));
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.TravelRequest).WithMany().HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.BookingType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BookingStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(200);
            entity.Property(e => e.BookingReference).HasMaxLength(200);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.HasIndex(e => e.TravelRequestId);
            entity.HasIndex(e => e.BookingStatus);
        });

        modelBuilder.Entity<TravelRequest>(entity =>
        {
            entity.ToTable("ea_travel_requests", "public", t =>
            {
                t.HasCheckConstraint("CK_ea_travel_requests_CurrentCycleNo_NonNegative", "\"CurrentCycleNo\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_requests_BusinessState", "\"BusinessState\" IN ('Draft', 'Upcoming', 'Active', 'Completed', 'Cancelled')");
                t.HasCheckConstraint("CK_ea_travel_requests_ApprovalState", "\"ApprovalState\" IN ('NotRequired', 'NotSubmitted', 'Pending', 'ChangesRequested', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_ea_travel_requests_TravelDates", "\"DepartureDate\" IS NULL OR \"ReturnDate\" IS NULL OR \"ReturnDate\" >= \"DepartureDate\"");
                t.HasCheckConstraint("CK_ea_travel_requests_HotelDates", "\"CheckInDate\" IS NULL OR \"CheckOutDate\" IS NULL OR \"CheckOutDate\" >= \"CheckInDate\"");
                // Zero is a legitimate supplied value (frontend-owned business data);
                // only a physically-impossible negative count is an integrity violation.
                t.HasCheckConstraint("CK_ea_travel_requests_NumberOfTravellers_NonNegative", "\"NumberOfTravellers\" IS NULL OR \"NumberOfTravellers\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_requests_NumberOfRooms_NonNegative", "\"NumberOfRooms\" IS NULL OR \"NumberOfRooms\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_requests_NumberOfGuests_NonNegative", "\"NumberOfGuests\" IS NULL OR \"NumberOfGuests\" >= 0");
                t.HasCheckConstraint("CK_ea_travel_requests_EstimatedCosts_NonNegative", "(\"EstimatedTravelCost\" IS NULL OR \"EstimatedTravelCost\" >= 0) AND (\"EstimatedHotelCost\" IS NULL OR \"EstimatedHotelCost\" >= 0) AND (\"EstimatedLocalTransportCost\" IS NULL OR \"EstimatedLocalTransportCost\" >= 0) AND (\"EstimatedHospitalityCost\" IS NULL OR \"EstimatedHospitalityCost\" >= 0)");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.ReferenceNo).HasColumnType("varchar(40)").IsRequired();
            entity.Property(e => e.EaTaskId).HasColumnType("bigint");
            entity.Property(e => e.CurrentCycleNo).HasDefaultValue(0);
            entity.Property(e => e.Purpose).HasMaxLength(1000);
            entity.Property(e => e.TravelType).HasMaxLength(100);
            entity.Property(e => e.FromLocation).HasMaxLength(500);
            entity.Property(e => e.ToLocation).HasMaxLength(500);
            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.SpecialRequirements).HasMaxLength(4000);
            entity.Property(e => e.TransportType).HasMaxLength(100);
            entity.Property(e => e.ClassPreference).HasMaxLength(200);
            entity.Property(e => e.BookingRequirements).HasMaxLength(4000);
            entity.Property(e => e.Hotel).HasMaxLength(500);
            entity.Property(e => e.RoomPreference).HasMaxLength(500);
            entity.Property(e => e.LocationPreference).HasMaxLength(500);
            entity.Property(e => e.PickupLocation).HasMaxLength(500);
            entity.Property(e => e.DropLocation).HasMaxLength(500);
            entity.Property(e => e.VehiclePreference).HasMaxLength(500);
            entity.Property(e => e.ClientGuestDetails).HasMaxLength(4000);
            entity.Property(e => e.HospitalityRequirement).HasMaxLength(4000);
            entity.Property(e => e.MeetingEventPurpose).HasMaxLength(1000);
            entity.Property(e => e.SpecialArrangements).HasMaxLength(4000);
            entity.Property(e => e.ItineraryNotes).HasMaxLength(4000);
            entity.Property(e => e.AdditionalInstructions).HasMaxLength(4000);
            entity.Property(e => e.EstimatedTravelCost).HasColumnType("numeric(18,2)");
            entity.Property(e => e.EstimatedHotelCost).HasColumnType("numeric(18,2)");
            entity.Property(e => e.EstimatedLocalTransportCost).HasColumnType("numeric(18,2)");
            entity.Property(e => e.EstimatedHospitalityCost).HasColumnType("numeric(18,2)");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.ApprovalRequired).HasDefaultValue(false);
            entity.Property(e => e.ApproverId).HasMaxLength(100);
            entity.Property(e => e.ApproverNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.ApprovedBy).HasMaxLength(200);
            entity.Property(e => e.RejectedBy).HasMaxLength(200);
            entity.Property(e => e.BusinessState).HasMaxLength(30).IsRequired().HasDefaultValue("Draft");
            entity.Property(e => e.ApprovalState).HasMaxLength(30).IsRequired().HasDefaultValue("NotRequired");
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.HasOne(e => e.EaTask).WithMany().HasForeignKey(e => e.EaTaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ReferenceNo).IsUnique();
            entity.HasIndex(e => e.EaTaskId).IsUnique();
            entity.HasIndex(e => e.BusinessState);
            entity.HasIndex(e => e.ApprovalState);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.ApproverId);
            entity.HasIndex(e => e.RequiredDate);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => e.DepartureDate);
            entity.HasIndex(e => e.ModifiedDate);
        });

        modelBuilder.Entity<TravelTraveller>(entity =>
        {
            entity.ToTable("ea_travel_travellers", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.TravellerName).HasMaxLength(200);
            entity.Property(e => e.EmployeePersonId).HasMaxLength(100);
            entity.Property(e => e.Department).HasMaxLength(200);
            entity.Property(e => e.ContactInformation).HasMaxLength(500);
            entity.HasOne(e => e.TravelRequest).WithMany(r => r.Travellers)
                .HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.TravelRequestId, e.SortOrder }).HasDatabaseName("IX_ea_travel_travellers_TravelRequestId_SortOrder");
        });

        modelBuilder.Entity<TravelRequestCycle>(entity =>
        {
            entity.ToTable("ea_travel_request_cycles", "public", t =>
            {
                t.HasCheckConstraint("CK_ea_travel_request_cycles_CycleNo_Positive", "\"CycleNo\" > 0");
                t.HasCheckConstraint("CK_ea_travel_request_cycles_DecisionState", "\"DecisionState\" IS NULL OR \"DecisionState\" IN ('Pending', 'ChangesRequested', 'Approved', 'Rejected')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.SubmittedBy).HasMaxLength(100);
            entity.Property(e => e.ApproverId).HasMaxLength(100);
            entity.Property(e => e.ApproverNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.DecisionState).HasMaxLength(30);
            entity.Property(e => e.ChangeReason).HasMaxLength(4000);
            entity.Property(e => e.ChangesMade).HasMaxLength(4000);
            entity.Property(e => e.DecisionComment).HasMaxLength(4000);
            entity.Property(e => e.DecisionBy).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.HasOne(e => e.TravelRequest).WithMany(e => e.Cycles).HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TravelRequestId, e.CycleNo }).IsUnique().HasDatabaseName("UX_ea_travel_request_cycles_TravelRequestId_CycleNo");
            entity.HasIndex(e => e.TravelRequestId);
        });

        // Delegation — Step 1 persistence foundation. No service/API exists yet.
        modelBuilder.Entity<Delegation>(entity =>
        {
            entity.ToTable("ea_delegations", "public", t =>
                t.HasCheckConstraint("CK_ea_delegations_Status", "\"Status\" IN ('Pending', 'InProgress', 'Completed')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.ReferenceNo).HasColumnType("varchar(40)").IsRequired();
            entity.Property(e => e.EaTaskId).HasColumnType("bigint");
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.DoerId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DoerNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.AssigneeId).HasMaxLength(100);
            entity.Property(e => e.AssigneeNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.AssignedById).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AssignedByNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.DelegationType).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.SourceEntityId).HasMaxLength(200);
            entity.Property(e => e.SourceReference).HasMaxLength(200);
            entity.Property(e => e.AdditionalNotes).HasMaxLength(4000);
            entity.Property(e => e.CompletedById).HasMaxLength(100);
            entity.Property(e => e.CompletedByNameSnapshot).HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);

            entity.HasOne(e => e.EaTask).WithMany().HasForeignKey(e => e.EaTaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SourceBusinessModule).WithMany().HasForeignKey(e => e.SourceBusinessModuleId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ReferenceNo).IsUnique();
            entity.HasIndex(e => e.EaTaskId).IsUnique();
            entity.HasIndex(e => e.DoerId);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SourceBusinessModuleId);
            entity.HasIndex(e => e.SourceEntityId);
        });

        // EA Approval entities
        modelBuilder.Entity<Entities.EaFms.ApprovalRequest>(entity =>
        {
            entity.ToTable("ea_approval_requests", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.EaTaskId).HasColumnType("bigint");
            entity.Property(e => e.ReferenceNo).HasColumnType("varchar(40)").IsRequired();
            entity.Property(e => e.RequestTitle).HasMaxLength(500);
            entity.Property(e => e.RequestType).HasMaxLength(200);
            entity.Property(e => e.RequestedBy).HasMaxLength(100);
            entity.Property(e => e.Department).HasMaxLength(200);
            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Justification).HasMaxLength(4000);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.ApproverId).HasMaxLength(100);
            entity.Property(e => e.ApproverName).HasMaxLength(200);
            entity.Property(e => e.ApprovedBy).HasMaxLength(200);
            entity.Property(e => e.RejectedBy).HasMaxLength(200);
            entity.Property(e => e.WorkflowStatus).HasMaxLength(100);

            entity.Property(e => e.Amount).HasColumnType("numeric(18,2)");

            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.SubmittedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ApprovedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.RejectedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ClosedAt).HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.EaTaskId).IsUnique();
            entity.HasOne(e => e.EaTask)
                .WithMany()
                .HasForeignKey(e => e.EaTaskId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ReferenceNo).IsUnique();
            entity.HasIndex(e => e.WorkflowStatus);
            entity.HasIndex(e => e.ApproverId);
            entity.HasIndex(e => e.RequestedBy);
            entity.HasIndex(e => e.RequiredApprovalDate);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Entities.EaFms.ApprovalCycle>(entity =>
        {
            entity.ToTable("ea_approval_cycles", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.Property(e => e.CycleNo).IsRequired();
            entity.Property(e => e.SubmittedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.SubmittedBy).HasMaxLength(100);
            entity.Property(e => e.RequiredApprovalDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ApproverId).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(100);
            entity.Property(e => e.ChangeReason).HasMaxLength(2000);
            entity.Property(e => e.DecisionComment).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.ApprovalRequest)
                .WithMany()
                .HasForeignKey(e => e.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ApprovalRequestId, e.CycleNo }).IsUnique().HasDatabaseName("UX_ea_approval_cycles_ApprovalRequestId_CycleNo");
            entity.HasIndex(e => e.ApprovalRequestId);
        });

        // Approval documents and related approval-specific tables are not created here.
        // Use shared ea_attachments and ea_audit_logs for documents and history.

        // ApprovalHistory is intentionally not created; re-use ea_audit_logs/ea_workflow_history for history events.

        // ApprovalReminder is not created here; reminders use existing ea_notifications/ea_followups mechanisms.

        // ApprovalEscalation table is not created here. Use ea_escalations for escalation data.

        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.ToTable("ea_meetings", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingNumber).HasMaxLength(200);

            entity.Property(e => e.IntakeRequestId);
            entity.Property(e => e.WorkflowInstanceId);

            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.Purpose).HasMaxLength(2000);

            entity.Property(e => e.DoerIds).HasColumnType("text[]").IsRequired().HasDefaultValueSql("ARRAY[]::text[]");
            entity.Property(e => e.DoerNames).HasColumnType("text[]").IsRequired().HasDefaultValueSql("ARRAY[]::text[]");
            entity.ToTable("ea_meetings", "public", t => t.HasCheckConstraint("CK_ea_meetings_DoerPairs",
                "cardinality(\"DoerIds\") = cardinality(\"DoerNames\") AND array_position(\"DoerIds\", NULL) IS NULL AND array_position(\"DoerNames\", NULL) IS NULL"));
            entity.Property(e => e.CompletionMom).HasMaxLength(4000);
            entity.HasOne(e => e.CompletionPdfAttachment).WithMany().HasForeignKey(e => e.CompletionPdfAttachmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.MeetingType).HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(200);

            entity.Property(e => e.Source).HasMaxLength(200);
            entity.Property(e => e.SourceChannel).HasMaxLength(200);
            entity.Property(e => e.SourceReferenceId).HasMaxLength(200);

            entity.Property(e => e.MeetingDate);
            entity.Property(e => e.StartDateTime);
            entity.Property(e => e.EndDateTime);

            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.MeetingMode).HasMaxLength(100);
            entity.Property(e => e.MeetingLink).HasMaxLength(1000);

            entity.Property(e => e.OrganizerId).HasMaxLength(100);
            entity.Property(e => e.OrganizerName).HasMaxLength(200);

            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.StatusId);

            entity.Property(e => e.RequiredDate);
            entity.Property(e => e.AgendaDueAt);
            entity.Property(e => e.MinutesDueAt);

            entity.Property(e => e.IsConfidential);

            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.ArchivedAt);

            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);

            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingDate);
            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.OrganizerId);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.WorkflowInstanceId);
        });

        // Notifications
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("ea_notifications", "public"); // stable mapping for EA FMS notifications
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecipientId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RecipientName)
                .HasMaxLength(200);

            entity.Property(e => e.Type)
                .HasMaxLength(200);

            entity.Property(e => e.Title)
                .HasMaxLength(500);

            entity.Property(e => e.Message)
                .HasMaxLength(4000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.RecipientId);
            entity.HasIndex(e => e.IsRead);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedDate);
        });

        // Audit logs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("ea_audit_logs", "public"); // stable mapping for EA FMS audit logs
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ActorId)
                .HasMaxLength(100);

            entity.Property(e => e.ActorName)
                .HasMaxLength(200);

            entity.Property(e => e.ActionType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Module)
                .HasMaxLength(200);

            entity.Property(e => e.EntityName)
                .HasMaxLength(200);

            entity.Property(e => e.EntityId)
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            // store old/new values as jsonb for future audit consumption
            entity.Property(e => e.OldValues)
                .HasColumnType("jsonb");

            entity.Property(e => e.NewValues)
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.ActionType);
            entity.HasIndex(e => e.Module);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => e.CreatedDate);
        });

        // Standalone calendar events — exactly like Google Calendar. The EA types every entry
        // in herself; this is the only table the Calendar module reads or writes, never an
        // aggregation over Meeting/Delegation/Approval/Travel/Followup.
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.ToTable("ea_calendar_events", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.OrganizerEmployeeId).HasMaxLength(100);
            entity.Property(e => e.OrganizerName).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(4000);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);

            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.EventType);
        });

        // AI task tables — one per real AI task (not one shared/generic shape per module),
        // same convention SCIH already uses: SCIH_Analysis and SCIH_SolutionDesign are two
        // separate tables, each with columns matching that task's own real fields. Every
        // table below mirrors its response DTO field-for-field; jsonb is used only for a
        // genuinely repeating list of structured sub-items (a list of proposed actions/
        // options, a list of strings), exactly how SCIH/HRMS use jsonb for their own nested
        // list fields while keeping every scalar field a real typed column.
        // FK-only relationships (no navigation property added to keep these lean log
        // tables exactly as designed) — real relational constraints, not just an indexed
        // plain column, matching the convention every other child table in this file uses
        // (e.g. ApprovalCycle -> ApprovalRequest, DelegationPhaseTat -> Delegation).
        // Xmin-as-concurrency-token is applied only to the tables that ever get mutated
        // after insert (the ones with an Apply/Confirm step) — the purely advisory,
        // insert-once tables are never updated, so a concurrency token there would guard
        // against a race that can never happen.
        //
        // UseXminAsConcurrencyToken() is marked obsolete in favor of the generic
        // Property<uint>("xmin").IsRowVersion() pattern, but that generic pattern treats
        // xmin as a brand-new shadow property and generates a migration that tries to
        // ADD COLUMN "xmin" — which fails, because xmin already exists as a Postgres system
        // column on every table. UseXminAsConcurrencyToken() is still the only correct way
        // to map the existing system column without EF trying to create a duplicate.
#pragma warning disable CS0618
        modelBuilder.Entity<MeetingActionExtraction>(entity =>
        {
            entity.ToTable("ea_meeting_action_extractions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProposedActionsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.AppliedActionsJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Meeting>().WithMany().HasForeignKey(e => e.MeetingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<TravelOptionSuggestion>(entity =>
        {
            entity.ToTable("ea_travel_option_suggestions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProposedOptionsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.AppliedBookingIdsJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<TravelRequest>().WithMany().HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<TravelOptionComparison>(entity =>
        {
            entity.ToTable("ea_travel_option_comparisons", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ComparedOptionsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.AppliedBookingIdsJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<TravelRequest>().WithMany().HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<TravelItineraryDraft>(entity =>
        {
            entity.ToTable("ea_travel_itinerary_drafts", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<TravelRequest>().WithMany().HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<TravelChecklistDraft>(entity =>
        {
            entity.ToTable("ea_travel_checklist_drafts", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChecklistItemsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<TravelRequest>().WithMany().HasForeignKey(e => e.TravelRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<ApprovalReadinessCheck>(entity =>
        {
            entity.ToTable("ea_approval_readiness_checks", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MissingFieldsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.SuggestedDocumentsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<ApprovalRequest>().WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<ApprovalApproverRecommendation>(entity =>
        {
            entity.ToTable("ea_approval_approver_recommendations", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<ApprovalRequest>().WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<ApprovalStatusSummary>(entity =>
        {
            entity.ToTable("ea_approval_status_summaries", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<ApprovalRequest>().WithMany().HasForeignKey(e => e.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<DelegationOwnerSuggestion>(entity =>
        {
            entity.ToTable("ea_delegation_owner_suggestions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Delegation>().WithMany().HasForeignKey(e => e.DelegationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<DelegationDueDatePrediction>(entity =>
        {
            entity.ToTable("ea_delegation_due_date_predictions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Basis).IsRequired().HasMaxLength(30);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Delegation>().WithMany().HasForeignKey(e => e.DelegationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<DelegationDelayRiskCheck>(entity =>
        {
            entity.ToTable("ea_delegation_delay_risk_checks", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RiskLevel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Delegation>().WithMany().HasForeignKey(e => e.DelegationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        // Calendar AI — quick-add (free-text -> structured event, suggest+apply) and
        // conflict-check (read-only advisory over the EA's own real calendar rows).
        modelBuilder.Entity<CalendarQuickAddSuggestion>(entity =>
        {
            entity.ToTable("ea_calendar_quick_add_suggestions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InputText).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.SuggestedTitle).HasMaxLength(500);
            entity.Property(e => e.SuggestedEventType).HasMaxLength(50);
            entity.Property(e => e.SuggestedLocation).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<CalendarEvent>().WithMany().HasForeignKey(e => e.AppliedCalendarEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });
#pragma warning restore CS0618

        modelBuilder.Entity<CalendarConflictCheck>(entity =>
        {
            entity.ToTable("ea_calendar_conflict_checks", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConflictsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.CreatedDate);
        });

        // Followup/Escalation AI — reminder draft (suggest+send, wires up the real
        // IEaReminderEmailSender), escalation suggestion (suggest+apply, wires up the real
        // EscalationService), resolution-time prediction and at-risk check (preview only).
#pragma warning disable CS0618
        modelBuilder.Entity<FollowupReminderSuggestion>(entity =>
        {
            entity.ToTable("ea_followup_reminder_suggestions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Followup>().WithMany().HasForeignKey(e => e.FollowupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });

        modelBuilder.Entity<FollowupEscalationSuggestion>(entity =>
        {
            entity.ToTable("ea_followup_escalation_suggestions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Followup>().WithMany().HasForeignKey(e => e.FollowupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Escalation>().WithMany().HasForeignKey(e => e.AppliedEscalationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
            entity.UseXminAsConcurrencyToken();
        });
#pragma warning restore CS0618

        modelBuilder.Entity<FollowupResolutionPrediction>(entity =>
        {
            entity.ToTable("ea_followup_resolution_predictions", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Basis).IsRequired().HasMaxLength(30);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Followup>().WithMany().HasForeignKey(e => e.FollowupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<FollowupAtRiskCheck>(entity =>
        {
            entity.ToTable("ea_followup_at_risk_checks", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RiskLevel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne<Followup>().WithMany().HasForeignKey(e => e.FollowupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.CreatedDate);
        });

        // Attachments (file metadata only)
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("ea_attachments", "public"); // stable mapping for EA FMS attachments
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RelatedModule)
                .HasMaxLength(200);

            entity.Property(e => e.RelatedEntity)
                .HasMaxLength(200);

            entity.Property(e => e.RelatedEntityId)
                .HasMaxLength(200);

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ObjectKey)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.ContentType)
                .HasMaxLength(200);

            entity.Property(e => e.UploadedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.AccessUrl)
                .HasMaxLength(1000);

            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb");

            entity.HasIndex(e => e.RelatedModule);
            entity.HasIndex(e => e.RelatedEntityId);
            entity.HasIndex(e => e.UploadedBy);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.UploadedAt);
        });

        modelBuilder.Entity<IntakeRequest>(entity =>
        {
            entity.ToTable("ea_intake_requests", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Description)
                .HasMaxLength(4000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Foreign keys to EA FMS catalogs
            entity.HasOne(e => e.BusinessModule)
                .WithMany()
                .HasForeignKey(e => e.BusinessModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Status)
                .WithMany()
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PriorityLevel)
                .WithMany()
                .HasForeignKey(e => e.PriorityLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BusinessModuleId);
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.PriorityLevelId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<IntakeClassification>(entity =>
        {
            entity.ToTable("ea_intake_classifications", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Details)
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(e => e.IntakeRequest)
                .WithMany(r => r.Classifications)
                .HasForeignKey(e => e.IntakeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("ea_workflow_instances", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StartedAt)
                .IsRequired();

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Relationship to IntakeRequest
            entity.HasOne(e => e.IntakeRequest)
                .WithMany()
                .HasForeignKey(e => e.IntakeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship to Status catalog representing current workflow state
            entity.HasOne(e => e.Status)
                .WithMany()
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.StartedAt);
            entity.Property(e => e.DoerId).HasMaxLength(100);
            entity.Property(e => e.DoerName).HasMaxLength(200);
            entity.Property(e => e.ArchivedAt);
            entity.Property(e => e.TatStartedAt);
            entity.HasIndex(e => e.DoerId);
            entity.HasIndex(e => e.TatStartedAt);
        });

        modelBuilder.Entity<WorkflowHistory>(entity =>
        {
            entity.ToTable("ea_workflow_history", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ChangedAt)
                .IsRequired();

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Notes)
                .HasMaxLength(2000);

            entity.HasOne(e => e.WorkflowInstance)
                .WithMany(w => w.History)
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.ToStatusId);
            entity.HasIndex(e => e.ChangedAt);
            entity.Property(e => e.StageOwnerId).HasMaxLength(100);
            entity.Property(e => e.StageOwnerName).HasMaxLength(200);
            entity.Property(e => e.TransitionType).HasMaxLength(50);
            entity.HasIndex(e => e.StageOwnerId);
        });

        modelBuilder.Entity<WorkPause>(entity =>
        {
            entity.ToTable("ea_work_pauses", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.WaitingOnId).HasMaxLength(100);
            entity.Property(e => e.WaitingOnName).HasMaxLength(200);
            entity.Property(e => e.WaitingOnExternal).HasMaxLength(500);
            entity.Property(e => e.ResponseOwnerId).HasMaxLength(100);
            entity.Property(e => e.ResponseOwnerName).HasMaxLength(200);
            entity.Property(e => e.ResumedById).HasMaxLength(100);
            entity.Property(e => e.ResumedByName).HasMaxLength(200);
            entity.Property(e => e.ResumedReason).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.IntakeRequest)
                .WithMany()
                .HasForeignKey(e => e.IntakeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkflowInstance)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Followup)
                .WithMany()
                .HasForeignKey(e => e.FollowupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.FollowupId);
            entity.HasIndex(e => e.StartAt);
            entity.HasIndex(e => e.EndAt);
            entity.HasIndex(e => e.ResponseOwnerId);
        });

        modelBuilder.Entity<WorkAssignment>(entity =>
        {
            entity.ToTable("ea_work_assignments", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DoerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DoerName).HasMaxLength(200);
            entity.Property(e => e.AssignedById).HasMaxLength(100);
            entity.Property(e => e.AssignedByName).HasMaxLength(200);
            entity.Property(e => e.UnassignedById).HasMaxLength(100);
            entity.Property(e => e.UnassignedByName).HasMaxLength(200);
            entity.Property(e => e.AssignmentType).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.IntakeRequest)
                .WithMany()
                .HasForeignKey(e => e.IntakeRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WorkflowInstance)
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Followup)
                .WithMany()
                .HasForeignKey(e => e.FollowupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.FollowupId);
            entity.HasIndex(e => e.DoerId);
            entity.HasIndex(e => e.AssignedAt);
        });

        // Meeting related tables: agendas, attendees, minutes, decisions, actions
        modelBuilder.Entity<MeetingAgenda>(entity =>
        {
            entity.ToTable("ea_meeting_agendas", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingId);
            entity.Property(e => e.SequenceNumber);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.OwnerName).HasMaxLength(200);
            entity.Property(e => e.IsPrepared);
            entity.Property(e => e.PreparedAt);
            entity.Property(e => e.PreparedById).HasMaxLength(100);
            entity.Property(e => e.PreparedByName).HasMaxLength(200);
            entity.Property(e => e.RequiredBy);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.RequiredBy);
            entity.HasIndex(e => e.IsPrepared);
            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingAttendee>(entity =>
        {
            entity.ToTable("ea_meeting_attendees", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingId);
            entity.Property(e => e.ParticipantName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(300);
            entity.Property(e => e.Role).HasMaxLength(200);
            entity.Property(e => e.InvitationStatus).HasMaxLength(200);
            entity.Property(e => e.AttendanceStatus).HasMaxLength(200);
            entity.Property(e => e.ConfirmedAt);
            entity.Property(e => e.AttendedAt);
            entity.Property(e => e.Remarks).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.ConfirmedAt);
            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingMinutes>(entity =>
        {
            entity.ToTable("ea_meeting_minutes", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingId);
            entity.Property(e => e.Summary).HasMaxLength(4000);
            entity.Property(e => e.DiscussionNotes).HasMaxLength(4000);
            entity.Property(e => e.PreparedById).HasMaxLength(100);
            entity.Property(e => e.PreparedByName).HasMaxLength(200);
            entity.Property(e => e.PreparedAt);
            entity.Property(e => e.SubmittedById).HasMaxLength(100);
            entity.Property(e => e.SubmittedByName).HasMaxLength(200);
            entity.Property(e => e.SubmittedAt);
            entity.Property(e => e.Status).HasMaxLength(200);
            entity.Property(e => e.RevisionNumber);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingDecision>(entity =>
        {
            entity.ToTable("ea_meeting_decisions", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingId);
            entity.Property(e => e.Decision).HasMaxLength(4000);
            entity.Property(e => e.OwnerName).HasMaxLength(200);
            entity.Property(e => e.DecisionDate);
            entity.Property(e => e.DueDate);
            entity.Property(e => e.Status).HasMaxLength(200);
            entity.Property(e => e.DecisionRecordId).HasMaxLength(200);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeetingAction>(entity =>
        {
            entity.ToTable("ea_meeting_actions", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetingId);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(4000);
            // Opaque stable identity, same sizing convention as Delegation.DoerId/
            // AssignedById. Nullable — see the entity's own doc comment for why.
            entity.Property(e => e.DoerId).HasMaxLength(100);
            entity.Property(e => e.DoerName).HasMaxLength(200);
            entity.Property(e => e.Priority).HasMaxLength(100);
            entity.Property(e => e.DueDate);
            entity.Property(e => e.StartDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.AssigneeId).HasMaxLength(100);
            entity.Property(e => e.AssigneeName).HasMaxLength(200);
            entity.Property(e => e.DelegationType).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(200);
            entity.Property(e => e.ActionRecordId).HasMaxLength(200);
            entity.Property(e => e.AcknowledgedAt);
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.MeetingId);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Meeting)
                .WithMany()
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TatRule>(entity =>
        {
            entity.Property(e => e.CreatedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.CreatedByEmployeeName).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeName).HasMaxLength(100);
            entity.ToTable("ea_tat_rules", "public", t =>
                t.HasCheckConstraint("CK_ea_tat_rules_TatMinutes_Positive", "\"TatMinutes\" > 0"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.BusinessModuleId).HasColumnType("bigint").IsRequired();
            entity.Property(e => e.ModuleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(200);
            entity.Property(e => e.Subtype).HasMaxLength(200);
            entity.Property(e => e.TaskType).HasMaxLength(20);
            entity.Property(e => e.TatMinutes).HasColumnType("integer").IsRequired();
            entity.Property(e => e.IsActive);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.IsDeleted);

            entity.HasIndex(e => e.BusinessModuleId);
            // Normalized active combination uniqueness is an expression index in the focused migration.
            // Keep it out of EF's raw-column indexes: lower(btrim(...)) must be identical to lookup.
            entity.HasOne(e => e.BusinessModule).WithMany().HasForeignKey(e => e.BusinessModuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DelegationPhaseTat>(entity =>
        {
            entity.Property(e => e.StartedById).HasMaxLength(100);
            entity.Property(e => e.StartedByName).HasMaxLength(200);
            entity.Property(e => e.EndedById).HasMaxLength(100);
            entity.Property(e => e.EndedByName).HasMaxLength(200);
            entity.ToTable("ea_delegation_phase_tat", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(20);
            // Nullable: null while the phase exists but hasn't had its explicit Start action called yet
            // (Review/Rework open idle now; Actual is set immediately by StartAsync).
            entity.Property(e => e.StartedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.EndedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.TatUsedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.TatPausedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");
            entity.HasOne(e => e.Delegation).WithMany().HasForeignKey(e => e.DelegationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.DelegationId, e.TaskType, e.ReviewCycleNumber }).IsUnique();
            entity.HasIndex(e => e.DelegationId).IsUnique()
                .HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("UX_ea_delegation_phase_tat_OpenPhase");
        });

        modelBuilder.Entity<Entities.EaFms.ApprovalPhaseTat>(entity =>
        {
            entity.ToTable("ea_approval_phase_tat", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StartedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.EndedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.TatUsedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.TatPausedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");
            entity.HasOne(e => e.ApprovalRequest).WithMany().HasForeignKey(e => e.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ApprovalRequestId, e.TaskType, e.ReviewCycleNumber }).IsUnique();
            entity.HasIndex(e => e.ApprovalRequestId).IsUnique()
                .HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("UX_ea_approval_phase_tat_OpenPhase");
        });

        modelBuilder.Entity<EaTask>(entity =>
        {
            entity.ToTable("ea_tasks", "public", t =>
            {
                t.HasCheckConstraint("CK_ea_tasks_AllottedTatMinutes_Positive", "\"AllottedTatMinutes\" > 0");
                t.HasCheckConstraint("CK_ea_tasks_TatUsedMinutes_NonNegative", "\"TatUsedMinutes\" IS NULL OR \"TatUsedMinutes\" >= 0");
                t.HasCheckConstraint("CK_ea_tasks_ExecutionStatus_Valid",
                    "\"ExecutionStatus\" IN ('NotStarted', 'InProgress', 'Completed', 'Cancelled')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.BusinessModuleId).HasColumnType("bigint").IsRequired();
            entity.Property(e => e.ModuleName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.BusinessRecordId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Task).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Type).HasMaxLength(200);
            entity.Property(e => e.Subtype).HasMaxLength(200);
            entity.Property(e => e.AllottedTatMinutes).HasColumnType("integer").IsRequired(false);
            entity.Property(e => e.TatUsedMinutes).HasColumnType("integer").IsRequired(false);
            entity.Property(e => e.ExecutionStatus).HasMaxLength(20).IsRequired();
            entity.Property(e => e.StartedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");
            entity.HasOne(e => e.BusinessModule).WithMany().HasForeignKey(e => e.BusinessModuleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowInstance).WithMany().HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TatRule).WithMany().HasForeignKey(e => e.TatRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.BusinessModuleId);
            entity.HasIndex(e => e.BusinessRecordId);
            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.TatRuleId);
            entity.HasIndex(e => e.ExecutionStatus);
            entity.HasIndex(e => new { e.BusinessModuleId, e.BusinessRecordId });
        });

        modelBuilder.Entity<TaskReview>(entity =>
        {
            entity.ToTable("ea_task_reviews", "public", t =>
                t.HasCheckConstraint("CK_ea_task_reviews_ReviewStatus",
                    "\"ReviewStatus\" IN ('PendingReview', 'Approved', 'ReworkRequested')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.EaTaskId).HasColumnType("bigint").IsRequired();
            entity.Property(e => e.ReviewCycleNo).IsRequired();
            entity.Property(e => e.ReviewStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ReviewerId).HasMaxLength(100);
            entity.Property(e => e.ReviewerName).HasMaxLength(200);
            entity.Property(e => e.SubmittedById).HasMaxLength(100);
            entity.Property(e => e.SubmittedByName).HasMaxLength(200);
            entity.Property(e => e.SubmittedAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.ReviewedById).HasMaxLength(100);
            entity.Property(e => e.ReviewedByName).HasMaxLength(200);
            entity.Property(e => e.ReviewedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ReviewRemark).HasMaxLength(2000);
            entity.Property(e => e.ReworkRemark).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");

            entity.HasOne(e => e.EaTask).WithMany().HasForeignKey(e => e.EaTaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EaTaskId);
            entity.HasIndex(e => new { e.EaTaskId, e.ReviewCycleNo }).IsUnique().HasDatabaseName("UX_ea_task_reviews_EaTaskId_ReviewCycleNo");
        });

        // ==========================================
        // Notifications, Audit and Attachments
        // ==========================================
        // NOTE: Legacy/unprefixed mappings for notifications and audit logs
        // have been removed to avoid conflicting with the EA-specific
        // ea_ mappings defined earlier in this file. Attachment mapping
        // is defined in the ea_ attachment section above and should be
        // used instead.


        modelBuilder.Entity<IntakeRequest>(entity =>
        {
            entity.ToTable("ea_intake_requests", "public");
            entity.HasKey(e => e.Id);
            // Align mapping with IntakeRequest entity properties
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Description)
                .HasMaxLength(4000);

            entity.Property(e => e.BusinessModuleId);

            entity.Property(e => e.StatusId);

            entity.Property(e => e.PriorityLevelId);

            entity.Property(e => e.IsActive);

            entity.Property(e => e.IsDeleted);

            entity.Property(e => e.Source)
                .HasMaxLength(200);

            entity.Property(e => e.SourceChannel)
                .HasMaxLength(200);

            entity.Property(e => e.SourceReferenceId)
                .HasMaxLength(200);

            entity.Property(e => e.RequiredDate);

            entity.Property(e => e.IsConfidential);

            entity.Property(e => e.DoerId)
                .HasMaxLength(100);

            entity.Property(e => e.DoerName)
                .HasMaxLength(200);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Indexes
            entity.HasIndex(e => e.BusinessModuleId);
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.PriorityLevelId);
            entity.HasIndex(e => e.RequiredDate);
            entity.HasIndex(e => e.CreatedDate);
        });

        modelBuilder.Entity<IntakeClassification>(entity =>
        {
            entity.ToTable("ea_intake_classifications", "public");
            entity.HasKey(e => e.Id);
            // IntakeClassification entity defines Name and Details properties.
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Details)
                .HasMaxLength(2000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // index useful for lookups by Name
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("ea_workflow_instances", "public");
            entity.HasKey(e => e.Id);

            // Align mapping with WorkflowInstance entity properties
            entity.Property(e => e.StartedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt);

            entity.Property(e => e.CompletedAt);

            entity.Property(e => e.DoerId)
                .HasMaxLength(100);

            entity.Property(e => e.DoerName)
                .HasMaxLength(200);

            entity.Property(e => e.BusinessRecordId)
                .HasMaxLength(200);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Indexes for efficient queries
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.DoerId);
            entity.HasIndex(e => e.StartedAt);
        });

        modelBuilder.Entity<Status>(entity =>
        {
            entity.ToTable("ea_statuses", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.DisplayOrder);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<PriorityLevel>(entity =>
        {
            entity.ToTable("ea_priority_levels", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Level)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedBy)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<FollowupCycle>(entity =>
        {
            entity.Property(e => e.FollowedUpByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.FollowedUpByEmployeeName).HasMaxLength(100);
            entity.ToTable("ea_followup_cycles", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.FollowedUpAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.NextFollowupAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ExpectedResponseAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.Property(e => e.OutcomeCode).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Followup).WithMany().HasForeignKey(e => e.FollowupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.FollowupId, e.SequenceNumber }).IsUnique();
        });

        // Followups
        modelBuilder.Entity<Followup>(entity =>
        {
            entity.Property(e => e.CreatedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.CreatedByEmployeeName).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeId).HasMaxLength(100);
            entity.Property(e => e.ModifiedByEmployeeName).HasMaxLength(100);
            entity.ToTable("ea_followups", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EaTaskId);
            entity.HasOne(e => e.EaTask).WithMany().HasForeignKey(e => e.EaTaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EaTaskId).IsUnique();

            entity.Property(e => e.IntakeRequestId);
            entity.Property(e => e.BusinessModuleId);
            entity.Property(e => e.BusinessRecordId).HasMaxLength(200);
            entity.Property(e => e.WorkflowInstanceId);

            entity.Property(e => e.DueAt).IsRequired();
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Type).HasMaxLength(100);

            entity.Property(e => e.DoerId).HasMaxLength(100);
            entity.Property(e => e.DoerName).HasMaxLength(200);

            entity.Property(e => e.PriorityLevelId);
            entity.Property(e => e.ReminderAt);
            entity.Property(e => e.ReminderSendEmail).HasDefaultValue(false);
            entity.Property(e => e.ReminderSendWhatsApp).HasDefaultValue(false);
            entity.Property(e => e.ReminderRecipientUserId);
            entity.Property(e => e.ReminderWhatsAppNumber).HasMaxLength(50);
            entity.Property(e => e.ReminderRecipientEmployeeId).HasMaxLength(100);
            entity.Property(e => e.ReminderRecipientName).HasMaxLength(200);
            entity.Property(e => e.ReminderRecipientEmail).HasMaxLength(300);
            entity.Property(e => e.NextFollowupAt);

            entity.Property(e => e.WaitingOnId).HasMaxLength(100);
            entity.Property(e => e.WaitingOnName).HasMaxLength(200);
            entity.Property(e => e.WaitingOnExternal).HasMaxLength(200);

            entity.Property(e => e.ResponseOwnerId).HasMaxLength(100);
            entity.Property(e => e.ResponseOwnerName).HasMaxLength(200);
            entity.Property(e => e.ExpectedResponseAt);
            entity.Property(e => e.LastFollowupAt);
            entity.Property(e => e.CompletionNote).HasMaxLength(2000);
            entity.Property(e => e.OutcomeCode).HasMaxLength(100);

            entity.Property(e => e.CompletedById).HasMaxLength(100);
            entity.Property(e => e.CompletedByName).HasMaxLength(200);

            entity.Property(e => e.IsDeleted);

            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.BusinessModuleId);
            entity.HasIndex(e => e.DueAt);
        });

        // Followup phase TAT — mirrors DelegationPhaseTat's own mapping exactly (see that block
        // above), plus StartedById/StartedByName/EndedById/EndedByName columns Delegation's own
        // phase table doesn't have. Followup has no Review/Rework: TaskType is always "Actual",
        // ReviewCycleNumber always 0, so there is at most one row per Followup ever, and at most
        // one open (EndedAt IS NULL) row per Followup at a time.
        modelBuilder.Entity<FollowupPhaseTat>(entity =>
        {
            entity.ToTable("ea_followup_phase_tat", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StartedAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.EndedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.StartedById).HasMaxLength(100);
            entity.Property(e => e.StartedByName).HasMaxLength(200);
            entity.Property(e => e.EndedById).HasMaxLength(100);
            entity.Property(e => e.EndedByName).HasMaxLength(200);
            entity.Property(e => e.TatUsedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.TatPausedSeconds).HasPrecision(20, 7);
            entity.Property(e => e.CreatedBy).IsRequired(false).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp with time zone");
            entity.HasOne(e => e.Followup).WithMany().HasForeignKey(e => e.FollowupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.FollowupId, e.TaskType, e.ReviewCycleNumber }).IsUnique();
            entity.HasIndex(e => e.FollowupId).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_followup_phase_actual", "\"TaskType\" = 'Actual' AND \"ReviewCycleNumber\" = 0"));
        });

        // Followup reminder log — an explicit record that a reminder handoff happened (see
        // FollowupReminderLog's own doc comment). Immutable: one row per handoff, never updated.
        modelBuilder.Entity<FollowupReminderLog>(entity =>
        {
            entity.ToTable("ea_followup_reminder_log", "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Recipient).IsRequired().HasMaxLength(300);
            entity.Property(e => e.RecipientName).HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasColumnType("text");
            entity.Property(e => e.SentAt).HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.SentById).HasMaxLength(100);
            entity.Property(e => e.SentByName).HasMaxLength(200);
            entity.Property(e => e.CreatedDate).HasColumnType("timestamp with time zone");
            entity.HasOne(e => e.Followup).WithMany().HasForeignKey(e => e.FollowupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.FollowupId);
            entity.HasIndex(e => e.SentAt);
        });

        // Escalation levels (catalog). Mapped explicitly to the existing EA table; without this
        // EF convention would query the non-existent "EscalationLevels" table.
        modelBuilder.Entity<EscalationLevel>(entity =>
        {
            entity.ToTable("ea_escalation_levels", "public");
            entity.HasKey(e => e.Id).HasName("PK_escalation_levels");
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.HasIndex(e => e.Code).HasDatabaseName("IX_escalation_levels_Code");
            entity.HasIndex(e => e.Level).HasDatabaseName("IX_escalation_levels_Level");
        });

        // Escalations
        modelBuilder.Entity<Escalation>(entity =>
        {
            entity.ToTable("ea_escalations", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FollowupId);
            entity.Property(e => e.WorkflowInstanceId);
            entity.Property(e => e.IntakeRequestId);
            entity.Property(e => e.BusinessModuleId);
            entity.Property(e => e.BusinessRecordId).HasMaxLength(200);

            entity.Property(e => e.EscalationLevelId);
            entity.Property(e => e.InitiatedAt).IsRequired();
            entity.Property(e => e.ResolvedAt);
            entity.Property(e => e.Notes).HasMaxLength(2000);

            entity.Property(e => e.EscalatedToId).HasMaxLength(100);
            entity.Property(e => e.EscalatedToName).HasMaxLength(200);

            entity.Property(e => e.AcknowledgedAt);
            entity.Property(e => e.AcknowledgedById).HasMaxLength(100);
            entity.Property(e => e.AcknowledgedByName).HasMaxLength(200);
            entity.Property(e => e.AcknowledgementNote).HasMaxLength(2000);

            entity.Property(e => e.ResolvedById).HasMaxLength(100);
            entity.Property(e => e.ResolvedByName).HasMaxLength(200);
            entity.Property(e => e.ResolutionNote).HasMaxLength(2000);

            entity.Property(e => e.NextEscalationAt);
            entity.Property(e => e.NextEscalationLevelId);

            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedDate);

            entity.Property(e => e.IsDeleted);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.IntakeRequestId);
            entity.HasIndex(e => e.FollowupId);
            entity.HasIndex(e => e.EscalationLevelId);
            entity.HasIndex(e => e.NextEscalationLevelId);
            entity.HasIndex(e => e.NextEscalationAt);
        });

        // Work revisions
        modelBuilder.Entity<WorkRevision>(entity =>
        {
            entity.ToTable("ea_work_revisions", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.RevisionNumber).IsRequired();

            entity.Property(e => e.BusinessModuleId);
            entity.Property(e => e.BusinessRecordId).HasMaxLength(200);

            entity.Property(e => e.RequestedById).HasMaxLength(100);
            entity.Property(e => e.RequestedByName).HasMaxLength(200);
            entity.Property(e => e.RequestedAt).IsRequired();

            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.ReviewerRemarks).HasMaxLength(4000);

            entity.Property(e => e.RespondedById).HasMaxLength(100);
            entity.Property(e => e.RespondedByName).HasMaxLength(200);
            entity.Property(e => e.RespondedAt);
            entity.Property(e => e.ResponseRemarks).HasMaxLength(4000);

            entity.Property(e => e.ResolvedAt);
            entity.Property(e => e.ResolvedById).HasMaxLength(100);
            entity.Property(e => e.ResolvedByName).HasMaxLength(200);

            entity.Property(e => e.IsResolved);
            entity.Property(e => e.IsDeleted);

            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.RevisionNumber);
            entity.HasIndex(e => new { e.WorkflowInstanceId, e.RevisionNumber }).IsUnique().HasDatabaseName("UX_ea_work_revisions_WorkflowInstanceId_RevisionNumber");
        });

        // Work pauses
        modelBuilder.Entity<WorkPause>(entity =>
        {
            entity.ToTable("ea_work_pauses", "public");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IntakeRequestId);
            entity.Property(e => e.WorkflowInstanceId);
            entity.Property(e => e.FollowupId);

            entity.Property(e => e.StartAt).IsRequired();
            entity.Property(e => e.EndAt);

            entity.Property(e => e.Reason).HasMaxLength(2000);

            entity.Property(e => e.WaitingOnId).HasMaxLength(100);
            entity.Property(e => e.WaitingOnName).HasMaxLength(200);
            entity.Property(e => e.WaitingOnExternal).HasMaxLength(200);

            entity.Property(e => e.ResponseOwnerId).HasMaxLength(100);
            entity.Property(e => e.ResponseOwnerName).HasMaxLength(200);

            entity.Property(e => e.ExpectedResponseAt);
            entity.Property(e => e.RequestSentAt);

            entity.Property(e => e.ResumedById).HasMaxLength(100);
            entity.Property(e => e.ResumedByName).HasMaxLength(200);
            entity.Property(e => e.ResumedReason).HasMaxLength(2000);

            entity.Property(e => e.IsDeleted);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.WorkflowInstanceId).HasDatabaseName("IX_ea_work_pauses_WorkflowInstanceId");
            // filtered unique index to ensure only one open pause per workflow instance
            entity.HasIndex(e => e.WorkflowInstanceId)
                .IsUnique()
                .HasDatabaseName("UX_ea_work_pauses_WorkflowInstanceId_Open")
                .HasFilter("\"WorkflowInstanceId\" IS NOT NULL AND \"EndAt\" IS NULL AND \"IsDeleted\" = false");
        });
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

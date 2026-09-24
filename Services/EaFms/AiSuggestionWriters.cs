using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Writes one row per generated AI suggestion into its own task-specific table (see each
/// entity's own doc comment for why there is one table per real AI task, not a shared
/// generic shape), and marks the latest unapplied suggestion for a task as applied once the
/// EA actually acts on it. Static helpers rather than a DI service: every AI service already
/// holds the same EaFmsDbContext instance for the current request, and keeping this logic in
/// one place keeps the convention identical across all four AI services.
/// </summary>
public static class AiSuggestionWriters
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static string ToJson(object value) => JsonSerializer.Serialize(value, Options);

    // ---- Meeting: action extraction ----

    public static async Task LogMeetingActionExtractionAsync(EaFmsDbContext db, long meetingId, MeetingAiAnalysisResponseDto response, CancellationToken ct = default)
    {
        db.MeetingActionExtractions.Add(new MeetingActionExtraction
        {
            MeetingId = meetingId,
            MomUsed = response.MomUsed,
            PdfUsed = response.PdfUsed,
            WarningMessage = response.WarningMessage,
            ProposedActionsJson = ToJson(response.ProposedActions),
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkMeetingActionExtractionAppliedAsync(EaFmsDbContext db, long meetingId, MeetingAiActionsConfirmResponseDto appliedResponse, CancellationToken ct = default)
    {
        var latest = await db.MeetingActionExtractions
            .Where(s => s.MeetingId == meetingId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return; // Confirm called without a preceding extraction this session — nothing to link.
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedActionsJson = ToJson(appliedResponse.CreatedActions);
        await db.SaveChangesAsync(ct);
    }

    // ---- Travel: options suggest / compare ----

    public static async Task LogTravelOptionSuggestionAsync(EaFmsDbContext db, long travelRequestId, TravelAiOptionsSuggestionResponseDto response, CancellationToken ct = default)
    {
        db.TravelOptionSuggestions.Add(new TravelOptionSuggestion
        {
            TravelRequestId = travelRequestId,
            ProposedOptionsJson = ToJson(response.ProposedOptions),
            WarningMessage = response.WarningMessage,
            CanCreateBooking = response.CanCreateBooking,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task LogTravelOptionComparisonAsync(EaFmsDbContext db, long travelRequestId, TravelAiCompareOptionsResponseDto response, CancellationToken ct = default)
    {
        db.TravelOptionComparisons.Add(new TravelOptionComparison
        {
            TravelRequestId = travelRequestId,
            ComparedOptionsJson = ToJson(response.ComparedOptions),
            CanCreateBooking = response.CanCreateBooking,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Confirm can follow either Suggest or Compare — marks only the single most
    /// recently generated unapplied suggestion across both tables, never both, so an older
    /// ignored suggestion of the other kind is never mistakenly tagged as applied.</summary>
    public static async Task MarkLatestTravelOptionsAppliedAsync(EaFmsDbContext db, long travelRequestId, TravelAiOptionsConfirmResponseDto appliedResponse, CancellationToken ct = default)
    {
        var bookingIdsJson = ToJson(appliedResponse.CreatedBookings.Select(b => b.BookingId));

        var latestSuggestion = await db.TravelOptionSuggestions
            .Where(s => s.TravelRequestId == travelRequestId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new { s.Id, s.CreatedDate })
            .FirstOrDefaultAsync(ct);
        var latestComparison = await db.TravelOptionComparisons
            .Where(s => s.TravelRequestId == travelRequestId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new { s.Id, s.CreatedDate })
            .FirstOrDefaultAsync(ct);

        if (latestSuggestion is null && latestComparison is null) return;

        if (latestComparison is null || (latestSuggestion is not null && latestSuggestion.CreatedDate >= latestComparison.CreatedDate))
        {
            var row = await db.TravelOptionSuggestions.FirstAsync(s => s.Id == latestSuggestion!.Id, ct);
            row.IsApplied = true;
            row.AppliedAt = Clock.UtcNowTz;
            row.AppliedBookingIdsJson = bookingIdsJson;
        }
        else
        {
            var row = await db.TravelOptionComparisons.FirstAsync(s => s.Id == latestComparison.Id, ct);
            row.IsApplied = true;
            row.AppliedAt = Clock.UtcNowTz;
            row.AppliedBookingIdsJson = bookingIdsJson;
        }
        await db.SaveChangesAsync(ct);
    }

    // ---- Travel: itinerary / checklist (purely advisory — log only, never applied) ----

    public static async Task LogTravelItineraryDraftAsync(EaFmsDbContext db, long travelRequestId, TravelAiItineraryResponseDto response, CancellationToken ct = default)
    {
        db.TravelItineraryDrafts.Add(new TravelItineraryDraft
        {
            TravelRequestId = travelRequestId,
            Itinerary = response.Itinerary,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task LogTravelChecklistDraftAsync(EaFmsDbContext db, long travelRequestId, TravelAiChecklistResponseDto response, CancellationToken ct = default)
    {
        db.TravelChecklistDrafts.Add(new TravelChecklistDraft
        {
            TravelRequestId = travelRequestId,
            ChecklistItemsJson = ToJson(response.ChecklistItems),
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Approval: readiness (purely advisory) ----

    public static async Task LogApprovalReadinessCheckAsync(EaFmsDbContext db, long approvalRequestId, ApprovalAiReadinessResponseDto response, CancellationToken ct = default)
    {
        db.ApprovalReadinessChecks.Add(new ApprovalReadinessCheck
        {
            ApprovalRequestId = approvalRequestId,
            IsLikelyReady = response.IsLikelyReady,
            MissingFieldsJson = ToJson(response.MissingFields),
            SuggestedDocumentsJson = ToJson(response.SuggestedDocuments),
            Notes = response.Notes,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Approval: approver recommendation ----

    public static async Task LogApprovalApproverRecommendationAsync(EaFmsDbContext db, long approvalRequestId, ApprovalAiApproverSuggestionResponseDto response, CancellationToken ct = default)
    {
        db.ApprovalApproverRecommendations.Add(new ApprovalApproverRecommendation
        {
            ApprovalRequestId = approvalRequestId,
            RecommendedApproverName = response.RecommendedApproverName,
            HistoricalSampleSize = response.HistoricalSampleSize,
            Reasoning = response.Reasoning,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkApprovalApproverRecommendationAppliedAsync(EaFmsDbContext db, long approvalRequestId, ApplyApproverSuggestionRequestDto applied, CancellationToken ct = default)
    {
        var latest = await db.ApprovalApproverRecommendations
            .Where(s => s.ApprovalRequestId == approvalRequestId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedApproverId = applied.ApproverId;
        latest.AppliedApproverName = applied.ApproverName;
        await db.SaveChangesAsync(ct);
    }

    // ---- Approval: status summary (purely advisory) ----

    public static async Task LogApprovalStatusSummaryAsync(EaFmsDbContext db, long approvalRequestId, ApprovalAiStatusSummaryResponseDto response, CancellationToken ct = default)
    {
        db.ApprovalStatusSummaries.Add(new ApprovalStatusSummary
        {
            ApprovalRequestId = approvalRequestId,
            Summary = response.Summary,
            WorkflowStatus = response.WorkflowStatus,
            CurrentCycleNo = response.CurrentCycleNo,
            DueState = response.DueState,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Delegation: owner suggestion ----

    public static async Task LogDelegationOwnerSuggestionAsync(EaFmsDbContext db, long delegationId, DelegationAiOwnerSuggestionResponseDto response, CancellationToken ct = default)
    {
        db.DelegationOwnerSuggestions.Add(new DelegationOwnerSuggestion
        {
            DelegationId = delegationId,
            SuggestedDoerId = response.SuggestedDoerId,
            SuggestedDoerName = response.SuggestedDoerName,
            HistoricalSampleSize = response.HistoricalSampleSize,
            Reasoning = response.Reasoning,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkDelegationOwnerSuggestionAppliedAsync(EaFmsDbContext db, long delegationId, ApplySuggestedOwnerRequestDto applied, CancellationToken ct = default)
    {
        var latest = await db.DelegationOwnerSuggestions
            .Where(s => s.DelegationId == delegationId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedDoerId = applied.DoerId;
        latest.AppliedDoerName = applied.DoerName;
        await db.SaveChangesAsync(ct);
    }

    // ---- Delegation: due date prediction ----

    public static async Task LogDelegationDueDatePredictionAsync(EaFmsDbContext db, long delegationId, DelegationAiDueDatePredictionResponseDto response, CancellationToken ct = default)
    {
        db.DelegationDueDatePredictions.Add(new DelegationDueDatePrediction
        {
            DelegationId = delegationId,
            SuggestedDueDate = response.SuggestedDueDate,
            Basis = response.Basis,
            Explanation = response.Explanation,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkDelegationDueDatePredictionAppliedAsync(EaFmsDbContext db, long delegationId, ApplyPredictedDueDateRequestDto applied, CancellationToken ct = default)
    {
        var latest = await db.DelegationDueDatePredictions
            .Where(s => s.DelegationId == delegationId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedDueDate = applied.EndDate;
        await db.SaveChangesAsync(ct);
    }

    // ---- Delegation: delay risk (purely advisory) ----

    public static async Task LogDelegationDelayRiskCheckAsync(EaFmsDbContext db, long delegationId, DelegationAiDelayRiskResponseDto response, CancellationToken ct = default)
    {
        db.DelegationDelayRiskChecks.Add(new DelegationDelayRiskCheck
        {
            DelegationId = delegationId,
            RiskLevel = response.RiskLevel,
            Reasoning = response.Reasoning,
            SuggestedNudgeMessage = response.SuggestedNudgeMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Calendar: quick add ----

    public static async Task LogCalendarQuickAddSuggestionAsync(EaFmsDbContext db, string inputText, CalendarAiQuickAddResponseDto response, CancellationToken ct = default)
    {
        db.CalendarQuickAddSuggestions.Add(new CalendarQuickAddSuggestion
        {
            InputText = inputText,
            SuggestedTitle = response.SuggestedTitle,
            SuggestedEventType = response.SuggestedEventType,
            SuggestedStartDateTime = response.SuggestedStartDateTime,
            SuggestedEndDateTime = response.SuggestedEndDateTime,
            SuggestedIsAllDay = response.SuggestedIsAllDay,
            SuggestedLocation = response.SuggestedLocation,
            Reasoning = response.Reasoning,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkCalendarQuickAddSuggestionAppliedAsync(EaFmsDbContext db, long appliedCalendarEventId, CancellationToken ct = default)
    {
        var latest = await db.CalendarQuickAddSuggestions
            .Where(s => !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return; // Apply called without a preceding quick-add this session — nothing to link.
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedCalendarEventId = appliedCalendarEventId;
        await db.SaveChangesAsync(ct);
    }

    // ---- Calendar: conflict check (purely advisory) ----

    public static async Task LogCalendarConflictCheckAsync(EaFmsDbContext db, CalendarAiConflictCheckResponseDto response, CancellationToken ct = default)
    {
        db.CalendarConflictChecks.Add(new CalendarConflictCheck
        {
            FromDate = response.From,
            ToDate = response.To,
            HasConflicts = response.HasConflicts,
            ConflictsJson = ToJson(response.Conflicts),
            Summary = response.Summary,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Followup: reminder draft ----

    public static async Task LogFollowupReminderSuggestionAsync(EaFmsDbContext db, long followupId, FollowupAiReminderSuggestionResponseDto response, CancellationToken ct = default)
    {
        db.FollowupReminderSuggestions.Add(new FollowupReminderSuggestion
        {
            FollowupId = followupId,
            SuggestedSubject = response.SuggestedSubject,
            SuggestedBody = response.SuggestedBody,
            Reasoning = response.Reasoning,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkFollowupReminderSuggestionAppliedAsync(EaFmsDbContext db, long followupId, string recipientEmail, CancellationToken ct = default)
    {
        var latest = await db.FollowupReminderSuggestions
            .Where(s => s.FollowupId == followupId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return; // Send called without a preceding draft this session — nothing to link.
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedRecipientEmail = recipientEmail;
        await db.SaveChangesAsync(ct);
    }

    // ---- Followup: escalation suggestion ----

    public static async Task LogFollowupEscalationSuggestionAsync(EaFmsDbContext db, long followupId, FollowupAiEscalationSuggestionResponseDto response, CancellationToken ct = default)
    {
        db.FollowupEscalationSuggestions.Add(new FollowupEscalationSuggestion
        {
            FollowupId = followupId,
            RecommendedEscalationLevelId = response.RecommendedEscalationLevelId,
            RecommendedEscalationLevelName = response.RecommendedEscalationLevelName,
            Reasoning = response.Reasoning,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    public static async Task MarkFollowupEscalationSuggestionAppliedAsync(EaFmsDbContext db, long followupId, long appliedEscalationId, CancellationToken ct = default)
    {
        var latest = await db.FollowupEscalationSuggestions
            .Where(s => s.FollowupId == followupId && !s.IsApplied)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;
        latest.IsApplied = true;
        latest.AppliedAt = Clock.UtcNowTz;
        latest.AppliedEscalationId = appliedEscalationId;
        await db.SaveChangesAsync(ct);
    }

    // ---- Followup: resolution-time prediction (purely advisory) ----

    public static async Task LogFollowupResolutionPredictionAsync(EaFmsDbContext db, long followupId, FollowupAiResolutionPredictionResponseDto response, CancellationToken ct = default)
    {
        db.FollowupResolutionPredictions.Add(new FollowupResolutionPrediction
        {
            FollowupId = followupId,
            PredictedResolutionDate = response.PredictedResolutionDate,
            Basis = response.Basis,
            Explanation = response.Explanation,
            WarningMessage = response.WarningMessage,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- Followup: at-risk check (purely advisory) ----

    public static async Task LogFollowupAtRiskCheckAsync(EaFmsDbContext db, long followupId, FollowupAiAtRiskResponseDto response, CancellationToken ct = default)
    {
        db.FollowupAtRiskChecks.Add(new FollowupAtRiskCheck
        {
            FollowupId = followupId,
            RiskLevel = response.RiskLevel,
            Reasoning = response.Reasoning,
            SuggestedAction = response.SuggestedAction,
            CreatedBy = "system",
            CreatedDate = Clock.UtcNowTz,
        });
        await db.SaveChangesAsync(ct);
    }
}

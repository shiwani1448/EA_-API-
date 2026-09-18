using System.Globalization;
using System.Text.Json;
using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public partial class TravelRequestService
{
    /// <summary>
    /// Reads the complete Travel timeline from ea_audit_logs. Every write path in this
    /// module (TravelRequestService[.Lifecycle], TravelDocumentService, TravelBookingService,
    /// TravelExpenseService, TravelArrangementService) already writes Module="Travel" with
    /// EntityName/EntityId identifying either the TravelRequest itself or one of its child
    /// rows (booking/expense/hospitality/local-transport) — this method fans out across
    /// those EntityId sets and merges everything chronologically. Purely read-only.
    /// </summary>
    public async Task<List<TravelHistoryEventDto>> GetHistoryAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await _db.TravelRequests.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct)
            ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

        var parentId = parent.Id.ToString(CultureInfo.InvariantCulture);

        var bookingIds = await _db.TravelBookings.AsNoTracking()
            .Where(b => b.TravelRequestId == parent.Id)
            .Select(b => b.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync(ct);
        var expenseIds = await _db.TravelExpenses.AsNoTracking()
            .Where(x => x.TravelRequestId == parent.Id)
            .Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync(ct);
        var hospitalityIds = await _db.TravelHospitalityArrangements.AsNoTracking()
            .Where(x => x.TravelRequestId == parent.Id)
            .Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync(ct);
        var localTransportIds = await _db.TravelLocalTransports.AsNoTracking()
            .Where(x => x.TravelRequestId == parent.Id)
            .Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToListAsync(ct);

        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Module == "Travel" &&
                ((a.EntityName == "TravelRequest" && a.EntityId == parentId) ||
                 (a.EntityName == "TravelBooking" && bookingIds.Contains(a.EntityId)) ||
                 (a.EntityName == "TravelExpense" && expenseIds.Contains(a.EntityId)) ||
                 (a.EntityName == "TravelHospitality" && hospitalityIds.Contains(a.EntityId)) ||
                 (a.EntityName == "TravelLocalTransport" && localTransportIds.Contains(a.EntityId))))
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
            .ToListAsync(ct);

        return logs.Select(log => MapHistoryEvent(log, parent)).ToList();
    }

    private static TravelHistoryEventDto MapHistoryEvent(AuditLog log, TravelRequest parent)
    {
        using var oldDoc = TryParseJson(log.OldValues);
        using var newDoc = TryParseJson(log.NewValues);
        var oldRoot = oldDoc?.RootElement ?? default;
        var newRoot = newDoc?.RootElement ?? default;

        string? stateType = null;
        string? previousStatus = null;
        string? newStatus = null;
        int? cycleNo = null;
        string? comment = null;
        var metadata = new Dictionary<string, object?>();

        switch (log.ActionType)
        {
            case "TRAVEL_CREATE_DRAFT":
                stateType = "TravelBusiness";
                newStatus = GetString(newRoot, "businessState") ?? "Draft";
                break;

            case "TRAVEL_UPDATE_DRAFT":
            {
                // Only a genuine ApprovalState transition (toggling ApprovalRequired on a
                // still-unsubmitted draft) counts as a state change here; a plain field
                // edit with no state change is reported with stateType = null.
                var oldApproval = GetString(oldRoot, "approvalState");
                var newApproval = GetString(newRoot, "approvalState");
                if (oldApproval is not null && newApproval is not null && oldApproval != newApproval)
                {
                    stateType = "TravelApproval";
                    previousStatus = oldApproval;
                    newStatus = newApproval;
                }
                break;
            }

            case "TRAVEL_SUBMIT":
                // RecordTravelAction omits CurrentCycle entirely (JSON null-omission) when
                // Submit took the no-approval branch — that is how the two Submit outcomes
                // are told apart, without guessing.
                if (TryGetObject(newRoot, "currentCycle", out var submitCycle))
                {
                    stateType = "TravelApproval";
                    previousStatus = "NotSubmitted";
                    newStatus = "Pending";
                    cycleNo = GetInt(submitCycle, "cycleNo");
                }
                else
                {
                    stateType = "TravelBusiness";
                    previousStatus = "Draft";
                    newStatus = "Upcoming";
                }
                break;

            case "TRAVEL_REQUEST_CHANGES":
                stateType = "TravelApproval"; previousStatus = "Pending"; newStatus = "ChangesRequested";
                cycleNo = GetInt(newRoot, "currentCycle", "cycleNo");
                comment = GetString(newRoot, "currentCycle", "changeReason");
                break;

            case "TRAVEL_RESUBMIT":
                stateType = "TravelApproval"; previousStatus = "ChangesRequested"; newStatus = "Pending";
                cycleNo = GetInt(newRoot, "currentCycle", "cycleNo");
                comment = GetString(newRoot, "currentCycle", "changesMade") ?? GetString(newRoot, "changesMade");
                break;

            case "TRAVEL_APPROVE":
                stateType = "TravelApproval"; previousStatus = "Pending"; newStatus = "Approved";
                cycleNo = GetInt(newRoot, "currentCycle", "cycleNo");
                comment = GetString(newRoot, "currentCycle", "decisionComment");
                break;

            case "TRAVEL_REJECT":
                stateType = "TravelApproval"; previousStatus = "Pending"; newStatus = "Rejected";
                cycleNo = GetInt(newRoot, "currentCycle", "cycleNo");
                comment = GetString(newRoot, "currentCycle", "decisionComment");
                break;

            case "TRAVEL_START":
                stateType = "TravelBusiness"; previousStatus = "Upcoming"; newStatus = "Active";
                break;

            case "TRAVEL_COMPLETE":
                stateType = "TravelBusiness"; previousStatus = "Active"; newStatus = "Completed";
                break;

            case "TRAVEL_CANCEL":
                stateType = "TravelBusiness";
                previousStatus = GetString(oldRoot, "businessState");
                newStatus = "Cancelled";
                break;

            case "TRAVEL_DOCUMENT_UPLOAD":
            {
                cycleNo = GetInt(newRoot, "cycleNo");
                var category = GetString(newRoot, "documentCategory");
                if (category is not null) metadata["category"] = category;
                break;
            }

            case "TRAVEL_DOCUMENT_DELETE":
                if (GetLong(newRoot, "documentId") is { } deletedDocId) metadata["documentId"] = deletedDocId;
                break;

            case "TRAVEL_BOOKING_CREATE":
                stateType = "Booking";
                newStatus = GetString(newRoot, "bookingStatus");
                metadata["bookingId"] = ParseLong(log.EntityId);
                if (GetString(newRoot, "bookingType") is { } createdBookingType) metadata["bookingType"] = createdBookingType;
                comment = GetString(newRoot, "notes");
                break;

            case "TRAVEL_BOOKING_UPDATE":
            case "TRAVEL_BOOKING_CANCEL":
                stateType = "Booking";
                previousStatus = GetString(oldRoot, "bookingStatus");
                newStatus = GetString(newRoot, "bookingStatus") ?? (log.ActionType == "TRAVEL_BOOKING_CANCEL" ? "Cancelled" : null);
                metadata["bookingId"] = ParseLong(log.EntityId);
                if (GetString(newRoot, "bookingType") is { } bookingType) metadata["bookingType"] = bookingType;
                comment = GetString(newRoot, "notes");
                break;

            case "TRAVEL_EXPENSE_CREATE":
                stateType = "Expense";
                newStatus = GetString(newRoot, "status");
                metadata["expenseId"] = ParseLong(log.EntityId);
                if (GetString(newRoot, "category") is { } expenseCategory) metadata["category"] = expenseCategory;
                break;

            case "TRAVEL_EXPENSE_UPDATE":
                stateType = "Expense";
                previousStatus = GetString(oldRoot, "status");
                newStatus = GetString(newRoot, "status");
                metadata["expenseId"] = ParseLong(log.EntityId);
                break;

            case "TRAVEL_EXPENSE_SUBMIT":
                stateType = "Expense"; previousStatus = GetString(oldRoot, "status"); newStatus = "Submitted";
                metadata["expenseId"] = ParseLong(log.EntityId);
                break;

            case "TRAVEL_EXPENSE_APPROVE":
                stateType = "Expense"; previousStatus = "Submitted"; newStatus = "Approved";
                metadata["expenseId"] = ParseLong(log.EntityId);
                break;

            case "TRAVEL_EXPENSE_REJECT":
                stateType = "Expense"; previousStatus = "Submitted"; newStatus = "Rejected";
                metadata["expenseId"] = ParseLong(log.EntityId);
                comment = GetString(newRoot, "rejectionReason");
                break;

            case "TRAVEL_LOCAL_TRANSPORT_CREATE":
                stateType = "LocalTransport";
                newStatus = GetString(newRoot, "transportStatus");
                metadata["localTransportId"] = ParseLong(log.EntityId);
                comment = GetString(newRoot, "notes");
                break;

            case "TRAVEL_LOCAL_TRANSPORT_UPDATE":
                stateType = "LocalTransport";
                previousStatus = GetString(oldRoot, "transportStatus");
                newStatus = GetString(newRoot, "transportStatus");
                metadata["localTransportId"] = ParseLong(log.EntityId);
                comment = GetString(newRoot, "notes");
                break;

            case "TRAVEL_HOSPITALITY_CREATE":
                stateType = "Hospitality";
                newStatus = GetString(newRoot, "status");
                metadata["hospitalityId"] = ParseLong(log.EntityId);
                comment = GetString(newRoot, "notes");
                break;

            case "TRAVEL_HOSPITALITY_UPDATE":
                stateType = "Hospitality";
                previousStatus = GetString(oldRoot, "status");
                newStatus = GetString(newRoot, "status");
                metadata["hospitalityId"] = ParseLong(log.EntityId);
                comment = GetString(newRoot, "notes");
                break;

            // Any future/unrecognized Travel action is still listed (never dropped),
            // with stateType/previousStatus/newStatus left null rather than guessed.
        }

        comment ??= log.Description;

        return new TravelHistoryEventDto
        {
            AuditId = log.Id,
            TravelRequestId = parent.Id,
            TravelReferenceNo = parent.ReferenceNo,
            EaTaskId = parent.EaTaskId,
            CycleNo = cycleNo,
            Action = log.ActionType,
            StateType = stateType,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            PerformedBy = log.ActorName ?? log.ActorId,
            PerformedAt = log.OccurredAt,
            Comment = comment,
            Metadata = metadata.Count > 0 ? metadata : null
        };
    }

    private static JsonDocument? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json); } catch (JsonException) { return null; }
    }

    private static bool TryGetObject(JsonElement root, string property, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Object)
        {
            value = v;
            return true;
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static int? GetInt(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var v) ? v : null;
    }

    private static long? GetLong(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.ValueKind == JsonValueKind.Number && current.TryGetInt64(out var v) ? v : null;
    }

    private static long? ParseLong(string? s) =>
        long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : null;
}

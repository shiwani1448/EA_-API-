namespace Jarvis5.Dtos.EaFms;

public class ApproveTravelRequestDto
{
    public int ExpectedCycleNo { get; set; }
    public string? DecisionComment { get; set; }
}

public class RejectTravelRequestDto
{
    public int ExpectedCycleNo { get; set; }
    public string? DecisionComment { get; set; }
}

public class RequestTravelChangesDto
{
    public int ExpectedCycleNo { get; set; }
    public string? ChangeReason { get; set; }
}

public class ResubmitTravelRequestDto
{
    public int ExpectedCycleNo { get; set; }
    public string? ChangesMade { get; set; }
}

public class TravelActionResponseDto
{
    public long TravelRequestId { get; set; }
    public long EaTaskId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string BusinessState { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = string.Empty;
    public int CurrentCycleNo { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TravelCurrentCycleDto? CurrentCycle { get; set; }
}

public class TravelCurrentCycleDto
{
    public int CycleNo { get; set; }
    public string DecisionState { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverNameSnapshot { get; set; }
    public string? ChangeReason { get; set; }
    public string? ChangesMade { get; set; }
    public string? DecisionComment { get; set; }
    public string? DecisionBy { get; set; }
    public DateTime? DecisionAt { get; set; }
}

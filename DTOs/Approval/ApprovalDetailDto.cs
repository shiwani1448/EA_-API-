namespace Jarvis5.Dtos.Approval;

/// <summary>Aggregated data for the Approval screen: request info, latest
/// analysis/solution-design summaries, and the current + previous approval round.</summary>
public class ApprovalDetailDto
{
    public long RequestId { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string RequestStatus { get; set; } = string.Empty;

    public AnalysisSummaryDto? Analysis { get; set; }
    public SolutionDesignSummaryDto? SolutionDesign { get; set; }

    /// <summary>The round currently awaiting/most recently given a decision.</summary>
    public ApprovalRoundDto? CurrentRound { get; set; }

    /// <summary>The round before CurrentRound, if this is a later approval cycle
    /// — used to show "Previous Approval Date" / "Previous Decision".</summary>
    public ApprovalRoundDto? PreviousRound { get; set; }
}

public class AnalysisSummaryDto
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string ExecutiveSummary { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public class SolutionDesignSummaryDto
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

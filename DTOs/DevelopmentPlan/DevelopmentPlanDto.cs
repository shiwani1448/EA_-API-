using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.Approval;
using Jarvis5.Dtos.SolutionDesign;

namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Response for GET /api/development-plan/{requestId} — Request details,
/// Latest Analysis, Approved Solution Design and Approval details are read-only
/// context; Modules is what this module actually owns. Task history is not
/// included here — see GET /api/development-plan/{requestId}/history.</summary>
public class DevelopmentPlanDto
{
    public RequestDetailDto Request { get; set; } = new();
    public AnalysisDetailDto? LatestAnalysis { get; set; }
    public SolutionDesignDetailDto? ApprovedSolutionDesign { get; set; }
    public ApprovalRoundDto? ApprovalDetails { get; set; }
    public List<TaskModuleDetailDto> Modules { get; set; } = new();
}

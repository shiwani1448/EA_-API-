namespace Jarvis5.Dtos.SolutionDesign;

/// <summary>Exact shape of the AI-generated (and later user-edited) solution design.
/// Field names/casing follow the spec's "Expected JSON" verbatim.</summary>
public class SolutionDesignResultDto
{
    public SolutionOverviewDto SolutionOverview { get; set; } = new();
    public BusinessSolutionDto BusinessSolution { get; set; } = new();
    public List<ModuleBreakdownItemDto> ModuleBreakdown { get; set; } = new();
    public ReusableAssetsDto ReusableAssets { get; set; } = new();
    public NewComponentsDto NewComponents { get; set; } = new();
    public WorkflowDto Workflow { get; set; } = new();
    public List<UserRoleDto> UserRoles { get; set; } = new();
    public List<string> ValidationRules { get; set; } = new();
    public List<string> BusinessRules { get; set; } = new();
    public RiskAnalysisDto RiskAnalysis { get; set; } = new();
    public List<string> FutureEnhancements { get; set; } = new();
    public string DirectorRecommendation { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
}

public class SolutionOverviewDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BusinessSolutionDto
{
    public string FutureProcess { get; set; } = string.Empty;
}

public class ModuleBreakdownItemDto
{
    public string Module { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}

public class ReusableAssetsDto
{
    public List<string> Apis { get; set; } = new();
    public List<string> DatabaseTables { get; set; } = new();
    public List<string> Modules { get; set; } = new();
    public List<string> UiComponents { get; set; } = new();
    public List<string> Flowcharts { get; set; } = new();
    public List<string> Documents { get; set; } = new();
}

public class NewComponentsDto
{
    public List<string> Apis { get; set; } = new();
    public List<string> Tables { get; set; } = new();
    public List<string> UiScreens { get; set; } = new();
    public List<string> Reports { get; set; } = new();
    public List<string> Dashboards { get; set; } = new();
}

public class WorkflowDto
{
    public List<string> Steps { get; set; } = new();
}

public class UserRoleDto
{
    public string Role { get; set; } = string.Empty;
    public string Responsibility { get; set; } = string.Empty;
}

public class RiskAnalysisDto
{
    public List<string> Technical { get; set; } = new();
    public List<string> Business { get; set; } = new();
    public List<string> Operational { get; set; } = new();
    public List<string> Scalability { get; set; } = new();
}

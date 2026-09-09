namespace Jarvis5.Dtos.SolutionDesign;

/// <summary>One "previous approved/reviewed solution design" fed into the AI prompt
/// context, so the AI reuses existing company assets instead of designing duplicates.</summary>
public class PreviousSolutionDesignDto
{
    public long RequestId { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string SolutionTitle { get; set; } = string.Empty;
    public string SolutionDescription { get; set; } = string.Empty;
    public List<string> ModuleNames { get; set; } = new();
    public List<string> ReusableApis { get; set; } = new();
    public List<string> ReusableDatabaseTables { get; set; } = new();
    public List<string> ReusableModules { get; set; } = new();
    public List<string> ReusableUiComponents { get; set; } = new();
    public List<string> ReusableFlowcharts { get; set; } = new();
    public List<string> ReusableDocuments { get; set; } = new();
}

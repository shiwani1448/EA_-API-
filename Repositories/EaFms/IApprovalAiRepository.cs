namespace Jarvis5.Repositories.EaFms;

public interface IApprovalAiRepository
{
    /// <summary>Top 5 approvers (by count) among Approved requests in the given department,
    /// excluding the request currently being reasoned about. The only honest source of a
    /// "who approves this kind of thing" signal, since this system has no employee/role
    /// directory — never an org chart or authorization rule.</summary>
    Task<List<(string Approver, int Count)>> GetTopApproversByDepartmentAsync(long excludeApprovalRequestId, string department, CancellationToken ct);
}

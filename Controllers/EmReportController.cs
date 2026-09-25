using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/em-report")]
public class EmReportController(IEmReportService service, IEmEmployeeReportService employeeReport) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(EmReportOverviewResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] EmReportOverviewQueryDto query, CancellationToken ct) =>
        Ok(await service.GetOverviewAsync(query, ct));

    [HttpGet("attention")]
    [ProducesResponseType(typeof(EmReportAttentionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttention([FromQuery] EmReportOverviewQueryDto query, CancellationToken ct) =>
        Ok(await service.GetAttentionAsync(query, ct));

    /// <summary>One row per module with tasks in the cohort (ordered by BusinessModuleId). Empty list when the cohort has no tasks.</summary>
    [HttpGet("modules")]
    [ProducesResponseType(typeof(IReadOnlyList<EmReportModuleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModules([FromQuery] EmReportOverviewQueryDto query, CancellationToken ct) =>
        Ok(await service.GetModulesAsync(query, ct));

    /// <summary>Paginated drill-down: one row per existing EaTask in the cohort (newest created first). Filters are applied before paging.</summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(Jarvis5.Common.PagedResult<EmReportTaskRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks([FromQuery] EmReportTaskRegisterQueryDto query, CancellationToken ct) =>
        Ok(await service.GetTasksAsync(query, ct));

    // ------------------------------------------------------------------ Employee report (per employee, per week)

    /// <summary>KPIs for one employee (or the whole team when no employee is sent) for an ISO week (Mon–Sat, India time):
    /// the matrix (Summary of all FMS / Actual / Review / Rework / Meeting), per-module numbers, task status, TAT, carry-forward overdue
    /// and focus areas. Every count also carries its change versus the previous week.</summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(EmKpiResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis([FromQuery] EmEmployeeReportQueryDto query, CancellationToken ct) =>
        Ok(await employeeReport.GetKpisAsync(query, ct));

    /// <summary>Weekly chart data for the N weeks ending with the selected week (default 6).</summary>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(EmTrendResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrends([FromQuery] EmEmployeeTrendQueryDto query, CancellationToken ct) =>
        Ok(await employeeReport.GetTrendsAsync(query, ct));

    /// <summary>All work in one list: Delegation and Approval phases (Actual / Review / Rework), Meetings, Follow-ups and Travel,
    /// filterable by module, task type, status and performance (OnTime / Delayed / Overdue / Pending / NotMeasured).</summary>
    [HttpGet("work-items")]
    [ProducesResponseType(typeof(Jarvis5.Common.PagedResult<EmWorkItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkItems([FromQuery] EmWorkItemQueryDto query, CancellationToken ct) =>
        Ok(await employeeReport.GetWorkItemsAsync(query, ct));
}

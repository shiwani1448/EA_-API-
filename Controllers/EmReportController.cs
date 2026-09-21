using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/em-report")]
public class EmReportController(IEmReportService service) : ControllerBase
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
}

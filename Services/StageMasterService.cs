using Jarvis5.Common;
using Jarvis5.Dtos.DevelopmentPlan;
using Jarvis5.Repositories;

namespace Jarvis5.Services;

public class StageMasterService : IStageMasterService
{
    private readonly IStageMasterRepository _stageMasterRepository;

    public StageMasterService(IStageMasterRepository stageMasterRepository)
    {
        _stageMasterRepository = stageMasterRepository;
    }

    public async Task<List<StageMasterDto>> GetAllAsync(CancellationToken ct = default)
    {
        var stages = await _stageMasterRepository.GetAllActiveAsync(ct);
        return stages.Select(s => new StageMasterDto
        {
            Id = s.Id,
            StageName = s.StageName,
            DisplayOrder = s.DisplayOrder,
            IsTestingStage = s.IsTestingStage,
            TatHours = new TatHoursDto
            {
                Small = s.TatSmallHours,
                Medium = s.TatMediumHours,
                Large = s.TatLargeHours
            },
            Checklist = JsonHelper.DeserializeList<string>(s.ChecklistJson)
        }).ToList();
    }
}

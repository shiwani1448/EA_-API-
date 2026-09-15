using Jarvis5.Dtos.EaFms;
namespace Jarvis5.Services.EaFms;
public interface ITatRuleService
{
    Task<List<TatRuleDto>> QueryAsync(long? moduleId, string? type, string? subtype, CancellationToken ct);
    Task<TatRuleDto> GetAsync(long id, CancellationToken ct);
    Task<TatRuleDto> SaveAsync(long? id, SaveTatRuleDto dto, CancellationToken ct);
}

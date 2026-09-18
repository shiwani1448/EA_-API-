using Jarvis5.Dtos.EaFms;
namespace Jarvis5.Services.EaFms;

public interface ITravelExpenseService
{
    Task<TravelExpenseResponseDto> CreateAsync(long travelRequestId, SaveTravelExpenseDto dto, CancellationToken ct = default);
    Task<List<TravelExpenseResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default);
    Task<TravelExpenseResponseDto> GetAsync(long expenseId, CancellationToken ct = default);
    Task<TravelExpenseResponseDto> UpdateAsync(long expenseId, SaveTravelExpenseDto dto, CancellationToken ct = default);
    Task<TravelExpenseResponseDto> SubmitAsync(long expenseId, CancellationToken ct = default);
    Task<TravelExpenseResponseDto> ApproveAsync(long expenseId, CancellationToken ct = default);
    Task<TravelExpenseResponseDto> RejectAsync(long expenseId, RejectTravelExpenseDto? dto, CancellationToken ct = default);
    Task<TravelExpenseSummaryDto> SummaryAsync(long travelRequestId, CancellationToken ct = default);
}

using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface IMonthlyOccurrenceService {
    Task<MonthlyItemDTO> GetByIdAsync(Guid occurenceId);
    Task<MonthlyItemDTO> UpdateAsync(MonthlyItemDTO dto);
    Task ToggleTaskAsync(Guid occurenceId);
    Task DeleteTaskAsync(Guid occurenceId);
}

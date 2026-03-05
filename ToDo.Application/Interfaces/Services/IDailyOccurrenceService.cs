using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface IDailyOccurrenceService {
    Task<DailyItemDTO> GetByIdAsync(Guid occurenceId);
    Task<DailyItemDTO> UpdateAsync(DailyItemDTO dto);
    Task ToggleTaskAsync(Guid occurenceId);
    Task DeleteTaskAsync(Guid occurenceId);
}
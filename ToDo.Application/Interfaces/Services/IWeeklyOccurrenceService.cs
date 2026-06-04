using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface IWeeklyOccurrenceService {
    Task<WeeklyItemDTO> GetByIdAsync(Guid occurenceId);
    Task<WeeklyItemDTO> UpdateAsync(WeeklyItemDTO dto);
    Task ToggleTaskAsync(Guid occurenceId);
    Task DeleteTaskAsync(Guid occurenceId);
}

using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface IDailyPlanService {
    Task<List<DailyItemDTO>> GetAllDailyOfDateAsync(DateOnly date);
    Task<DailyItemDTO> AddTaskToDateAsync(DateOnly date, DailyItemDTO dto);
}
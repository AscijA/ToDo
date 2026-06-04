using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface IDailyPlanService {
    Task<List<DailyItemDTO>> GetAllDailyOfDateAsync(DateOnly date);
    Task<DailyItemDTO> AddTaskToDateAsync(DateOnly date, DailyItemDTO dto);

    Task<List<WeeklyItemDTO>> GetAllWeeklyAsync(DateOnly date);
    Task<WeeklyItemDTO> AddTaskToWeekAsync(DateOnly date, WeeklyItemDTO dto);

    Task<List<MonthlyItemDTO>> GetAllMonthlyAsync(DateOnly date);
    Task<MonthlyItemDTO> AddTaskToMonthAsync(DateOnly date, MonthlyItemDTO dto);
}

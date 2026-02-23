using System;
using System.Collections.Generic;
using System.Text;
using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface ITaskService {

    Task<DailyItemDTO> UpdateAsync(DailyItemDTO dto);
    Task<List<DailyItemDTO>> GetAllDailyOfDateAsync(DateOnly date);
    Task<DailyItemDTO> GetByIdAsync(Guid occurenceId);
    Task ToggleTaskAsync(Guid occurenceId);
    Task<DailyItemDTO> AddTaskToDateAsync(DateOnly date, DailyItemDTO dto);
    Task DeleteTaskAsync(Guid occurenceId);
}

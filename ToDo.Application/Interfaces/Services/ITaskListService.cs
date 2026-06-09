using ToDo.Application.Contracts.DTOs;

namespace ToDo.Application.Interfaces.Services;

public interface ITaskListService {
    Task<List<TaskListDto>> GetAllAsync();
    Task<TaskListDto?> GetByIdAsync(Guid id);
    Task<TaskListDto> CreateAsync(string name, string color, string? description);
    Task UpdateAsync(Guid id, string name, string color, string? description);
    Task DeleteAsync(Guid id);
    Task<TaskListItemDto> AddItemAsync(Guid listId, string text);
    Task UpdateItemAsync(TaskListItemDto item);
    Task DeleteItemAsync(Guid itemId);
    Task UpdateOrderAsync(Guid listId, List<Guid> itemIds);
    Task ReorderListsAsync(List<Guid> listIds);
}

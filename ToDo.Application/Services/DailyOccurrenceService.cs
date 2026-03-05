using Microsoft.EntityFrameworkCore;
using ToDo.Application.Contracts.DTOs;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Mappings;
using ToDo.Infrastructure.Data;

namespace ToDo.Application.Services;

public class DailyOccurrenceService : IDailyOccurrenceService {
    private readonly IDbContextFactory<TodoDbContext> _contextFactory;

    public DailyOccurrenceService(IDbContextFactory<TodoDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task<DailyItemDTO> GetByIdAsync(Guid id) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences.AsNoTracking()
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == id);
        return dailyItem?.ToDailyItem() ?? new();
    }

    public async Task<DailyItemDTO> UpdateAsync(DailyItemDTO dto) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .Include(x => x.TaskDefinition)
            .FirstOrDefaultAsync(x => x.Id == dto.OccurrenceId);
        if (dailyItem == null) {
            throw new Exception("Daily item not found");
        }

        dailyItem.TaskDefinition.Title = dto.Title;
        dailyItem.TaskDefinition.Description = dto.Description ?? "";
        dailyItem.IsDone = dto.IsDone;
        dailyItem.Timeslot = dto.Timeslot;
        await context.SaveChangesAsync();

        return dailyItem.ToDailyItem();
    }

    public async Task ToggleTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            dailyItem.IsDone = !dailyItem.IsDone;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteTaskAsync(Guid occurenceId) {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dailyItem = await context.DailyOccurrences
            .FirstOrDefaultAsync(x => x.Id == occurenceId);
        if (dailyItem != null) {
            context.DailyOccurrences.Remove(dailyItem);
            await context.SaveChangesAsync();
        }
    }
}
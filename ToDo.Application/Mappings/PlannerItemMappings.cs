using ToDo.Application.Contracts.DTOs;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Application.Mappings;

public static class PlannerItemMappings {
    public static DailyItemDTO ToDailyItem(this DailyOccurrence occurrence) {
        return new DailyItemDTO(
            occurrence.Id,
            occurrence.TaskDefinitionId,
            occurrence.TaskDefinition.Title,
            occurrence.TaskDefinition.Description,
            occurrence.IsDone,
            occurrence.Timeslot
        );
    }

    public static WeeklyItemDTO ToWeeklyItem(this WeeklyOccurrence occurrence) {
        return new WeeklyItemDTO(
            occurrence.Id,
            occurrence.TaskDefinitionId,
            occurrence.TaskDefinition.Title,
            occurrence.TaskDefinition.Description,
            occurrence.IsDone,
            occurrence.WeeklyPlan?.Date ?? DateOnly.MinValue,
            occurrence.DayOfWeek
        );
    }

    public static MonthlyItemDTO ToMonthlyItem(this MonthlyOccurrence occurrence) {
        return new MonthlyItemDTO(
            occurrence.Id,
            occurrence.TaskDefinitionId,
            occurrence.TaskDefinition.Title,
            occurrence.TaskDefinition.Description,
            occurrence.IsDone,
            occurrence.MonthlyPlan?.Date ?? DateOnly.MinValue,
            occurrence.DayOfMonth
        );
    }
}

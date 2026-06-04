namespace ToDo.Application.Contracts.DTOs;

public class WeeklyItemDTO : PlannerItemDTO {
    public WeeklyItemDTO() {
    }

    public WeeklyItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone, DateOnly date, DayOfWeek? weekDay, string? color = null)
        : base(occurrenceId, taskDefinitionId, title, description, isDone, color) {
        Date = date;
        WeekDay = weekDay;
    }

    public DateOnly Date { get; set; }
    public DayOfWeek? WeekDay { get; set; }
}

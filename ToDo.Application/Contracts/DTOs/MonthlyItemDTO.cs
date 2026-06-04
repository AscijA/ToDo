namespace ToDo.Application.Contracts.DTOs;

public class MonthlyItemDTO : PlannerItemDTO {
    public MonthlyItemDTO() {
    }

    public MonthlyItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone, DateOnly date, int? dayOfMonth, string? color = null)
        : base(occurrenceId, taskDefinitionId, title, description, isDone, color) {
        Date = date;
        DayOfMonth = dayOfMonth;
    }

    public DateOnly Date { get; set; }
    public int? DayOfMonth { get; set; }
}

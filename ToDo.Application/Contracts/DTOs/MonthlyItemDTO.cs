namespace ToDo.Application.Contracts.DTOs;

public class MonthlyItemDTO : PlannerItemDTO {
    public MonthlyItemDTO() {
    }

    public MonthlyItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone, DateOnly date, int? dayOfMonth)
        : base(occurrenceId, taskDefinitionId, title, description, isDone) {
        Date = date;
        DayOfMonth = dayOfMonth;
    }

    public DateOnly Date { get; set; }
    public int? DayOfMonth { get; set; }
}

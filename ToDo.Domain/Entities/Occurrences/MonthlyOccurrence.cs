using ToDo.Domain.Entities.Plans;

namespace ToDo.Domain.Entities.Occurrences;

public class MonthlyOccurrence : OccurrenceBase {
    public Guid MonthlyPlanId { get; set; }
    public MonthlyPlan MonthlyPlan { get; set; } = null!;

    public int? DayOfMonth { get; set; }
}

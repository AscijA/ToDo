using ToDo.Domain.Entities.Plans;

namespace ToDo.Domain.Entities.Occurrences;

public class WeeklyOccurrence : OccurrenceBase {
    public Guid WeeklyPlanId { get; set; }
    public WeeklyPlan WeeklyPlan { get; set; } = null!;
}
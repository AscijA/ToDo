using ToDo.Domain.Entities.Plans;

namespace ToDo.Domain.Entities.Occurrences;

public class DailyOccurrence : OccurrenceBase {
    // Foreign keys
    public Guid DailyPlanId { get; set; }
    public DailyPlan DailyPlan { get; set; } = null!;

    public string? Timeslot { get; set; } 
    public int SortOrder { get; set; }

    public DailyOccurrence() { }

}
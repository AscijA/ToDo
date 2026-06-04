using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Domain.Entities.Plans;

public class WeeklyPlan : PlanBase {

    public ICollection<WeeklyOccurrence> Occurrences { get; set; } = new List<WeeklyOccurrence>();
}

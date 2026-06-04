using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Domain.Entities.Plans;

public class DailyPlan : PlanBase {

    public ICollection<DailyOccurrence> Occurrences { get; set; } = new List<DailyOccurrence>();
}
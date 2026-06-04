using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Domain.Entities.Plans;

public class MonthlyPlan : PlanBase {
    public ICollection<MonthlyOccurrence> Occurrences { get; set; } = new List<MonthlyOccurrence>();
}

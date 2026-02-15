using ToDo.Domain.Entities.Occurences;

namespace ToDo.Domain.Entities;

public class DailyPlan : EntityBase {
    public DateOnly Date { get; set; }

    public ICollection<DailyOccurrence> Occurrences { get; set; } = new List<DailyOccurrence>();
}
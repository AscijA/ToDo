using System;
using System.Collections.Generic;
using System.Text;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Domain.Entities.Plans;

public class WeeklyPlan : PlanBase {
    public string Week { get; set; } = string.Empty;

    public ICollection<WeeklyOccurrence> Occurrences { get; set; } = new List<WeeklyOccurrence>();
}

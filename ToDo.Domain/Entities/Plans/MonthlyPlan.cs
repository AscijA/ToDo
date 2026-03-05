using System;
using System.Collections.Generic;
using System.Text;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Domain.Entities.Plans;

public class MonthlyPlan : PlanBase {
    public string Month { get; set; } = string.Empty;

    public ICollection<MonthlyOccurrence> Occurrences { get; set; } = new List<MonthlyOccurrence>();
}

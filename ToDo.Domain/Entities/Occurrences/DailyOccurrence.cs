using ToDo.Domain.Entities;

namespace ToDo.Domain.Entities.Occurrences;

public class DailyOccurrence : EntityBase {
    // Foreign keys
    public Guid DailyPlanId { get; set; }
    public DailyPlan DailyPlan { get; set; } = null!;

    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;

    public string? Timeslot { get; set; } 
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }

    public DailyOccurrence() { }

}
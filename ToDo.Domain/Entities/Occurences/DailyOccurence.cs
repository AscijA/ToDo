using ToDo.Domain.Entities;

namespace ToDo.Domain.Entities.Occurences;

public class DailyOccurrence : EntityBase {
    // Foreign keys
    public Guid DailyPlanId { get; set; }
    public DailyPlan DailyPlan { get; set; } = null!;

    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;

    public int? StartMinutes { get; set; }
    public int? EndMinutes { get; set; }

    public DateTimeOffset? DoneAtUtc { get; set; }

    public int SortOrder { get; set; }

    public bool IsDone => DoneAtUtc != null;

    public TimeOnly? StartTime => StartMinutes is null ? null : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(StartMinutes.Value));
    public TimeOnly? EndTime => EndMinutes is null ? null : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(EndMinutes.Value));
}
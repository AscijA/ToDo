
namespace ToDo.Domain.Entities.Occurences;

public class WeeklyTaskState : EntityBase {
    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;

    public string WeekKey { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public bool IsCompleted => CompletedAtUtc != null;
}
namespace ToDo.Domain.Entities.Occurences;

public class MonthlyTaskState : EntityBase {
    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;

    public string MonthKey { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public bool IsCompleted => CompletedAtUtc != null;
}
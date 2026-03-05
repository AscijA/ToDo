namespace ToDo.Domain.Entities.Occurrences;

public abstract class OccurrenceBase : EntityBase {
    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;
    public bool IsDone { get; set; }
}
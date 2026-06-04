namespace ToDo.Domain.Entities;

public class TaskListItem : EntityBase {
    public Guid TaskDefinitionId { get; set; }
    public TaskDefinition TaskDefinition { get; set; } = null!;

    public bool IsDone { get; set; }

    public Guid TaskListId { get; set; }
    public TaskList? TaskList { get; set; }

    public TaskListItem() { }
}

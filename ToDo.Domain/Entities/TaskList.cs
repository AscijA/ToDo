namespace ToDo.Domain.Entities;

public class TaskList : EntityBase {
    public string Name { get; set; } = null!;
    public string Color { get; set; } = "#594AE2"; // Default MudBlazor Primary
    public string? Description { get; set; }
    public int Position { get; set; }

    private readonly List<TaskListItem> _items = new();
    public IReadOnlyCollection<TaskListItem> Items => _items;

    public TaskList() { }
}

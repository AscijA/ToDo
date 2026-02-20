
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.ValueObjects;

namespace ToDo.Domain.Entities;

public class TaskDefinition : EntityBase {
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Bucket Bucket { get; set; }

    private readonly List<DailyOccurrence> _dailyOccurrences = new();
    private readonly List<WeeklyTaskState> _weeklyTaskStates = new();
    private readonly List<MonthlyTaskState> _monthlyTaskStates = new();
    public IReadOnlyCollection<DailyOccurrence> DailyOccurrences => _dailyOccurrences;
    public IReadOnlyCollection<WeeklyTaskState> WeeklyTaskStates => _weeklyTaskStates;
    public IReadOnlyCollection<MonthlyTaskState> MonthlyTaskStates => _monthlyTaskStates;

    public TaskDefinition() {
    }

}

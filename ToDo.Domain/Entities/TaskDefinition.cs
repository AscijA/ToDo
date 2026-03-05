
using ToDo.Domain.Entities.Occurrences;
using ToDo.Domain.ValueObjects;

namespace ToDo.Domain.Entities;

public class TaskDefinition : EntityBase {
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Bucket Bucket { get; set; }

    private readonly List<DailyOccurrence> _dailyOccurrences = new();
    private readonly List<WeeklyOccurrence> _weeklyOccurrences = new();
    private readonly List<MonthlyOccurrence> _monthlyOccurrences = new();
    public IReadOnlyCollection<DailyOccurrence> DailyOccurrences => _dailyOccurrences;
    public IReadOnlyCollection<WeeklyOccurrence> WeeklyOccurrences => _weeklyOccurrences;
    public IReadOnlyCollection<MonthlyOccurrence> MonthlyOccurrences => _monthlyOccurrences;

    public TaskDefinition() {
    }

}

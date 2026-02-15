
using ToDo.Domain.Entities.Occurences;
using ToDo.Domain.ValueObjects;

namespace ToDo.Domain.Entities;

public class TaskDefinition : EntityBase {
    public string Title { get;  set; }
    public string Description { get; set; }
    public Bucket Bucket { get; set; }

    private readonly List<DailyOccurence> _dailyOccurences = new();
    private readonly List<WeeklyOccurence> _weeklyOccurences = new();
    private readonly List<MonthlyOccurence> _monthlyOccurences = new();
    IReadOnlyCollection<DailyOccurence> DailyOccurences { get; set; }
    IReadOnlyCollection<WeeklyOccurence> WeeklyOccurences { get; set; }
    IReadOnlyCollection<MonthlyOccurence> MonthlyOccurences { get; set; }

    public TaskDefinition() {
    }

}

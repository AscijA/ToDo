using System.ComponentModel.DataAnnotations;

namespace ToDo.Application.Contracts.DTOs;

public class DailyItemDTO {
    

    public DailyItemDTO() {
    }

    public DailyItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone, string? timeslot) {
        OccurrenceId = occurrenceId;
        TaskDefinitionId = taskDefinitionId;
        Title = title;
        Description = description;
        IsDone = isDone;
        Timeslot = timeslot;
    }

    public Guid OccurrenceId;
    public Guid TaskDefinitionId;

    [Required(ErrorMessage = "Required")]
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDone { get; set; }

    [Required(ErrorMessage = "Required")]
    public string? Timeslot { get; set; }
}

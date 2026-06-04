using System.ComponentModel.DataAnnotations;

namespace ToDo.Application.Contracts.DTOs;

public class DailyItemDTO : PlannerItemDTO {
    public DailyItemDTO() {
    }

    public DailyItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone, string? timeslot, string? color = null)
        : base(occurrenceId, taskDefinitionId, title, description, isDone, color) {
        Timeslot = timeslot;
    }

    [Required(ErrorMessage = "Required")]
    public string? Timeslot { get; set; }
}

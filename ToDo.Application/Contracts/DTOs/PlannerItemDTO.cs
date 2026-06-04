using System.ComponentModel.DataAnnotations;

namespace ToDo.Application.Contracts.DTOs;

public class PlannerItemDTO {
    public PlannerItemDTO() {
    }

    public PlannerItemDTO(Guid occurrenceId, Guid taskDefinitionId, string title, string? description, bool isDone) {
        OccurrenceId = occurrenceId;
        TaskDefinitionId = taskDefinitionId;
        Title = title;
        Description = description;
        IsDone = isDone;
    }

    public Guid OccurrenceId;
    public Guid TaskDefinitionId;

    [Required(ErrorMessage = "Required")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; } = "";
    public bool IsDone { get; set; }
}

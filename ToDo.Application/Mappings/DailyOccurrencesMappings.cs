using ToDo.Application.Contracts.DTOs;
using ToDo.Domain.Entities.Occurrences;

namespace ToDo.Application.Mappings;

public static class DailyOccurrencesMappings {

    public static DailyItemDTO ToDailyItem(this DailyOccurrence occurrence) {
        return new DailyItemDTO(
            occurrence.Id,
            occurrence.TaskDefinitionId,
            occurrence.TaskDefinition.Title,
            occurrence.TaskDefinition.Description,
            occurrence.IsDone,
            occurrence.Timeslot
        );
    }
}

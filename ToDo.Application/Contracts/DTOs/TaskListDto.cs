using System.Collections.Generic;

namespace ToDo.Application.Contracts.DTOs;

public record TaskListItemDto(Guid Id, Guid TaskDefinitionId, string Text, bool IsDone);
public record TaskListDto(Guid Id, string Name, string Color, string? Description, List<TaskListItemDto> Items);

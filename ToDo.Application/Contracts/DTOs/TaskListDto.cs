using System.Collections.Generic;

namespace ToDo.Application.Contracts.DTOs;

public record TaskListItemDto(Guid Id, Guid TaskDefinitionId, string Text, bool IsDone, int Position);
public record TaskListDto(Guid Id, string Name, string Color, string? Description, int Position, List<TaskListItemDto> Items);

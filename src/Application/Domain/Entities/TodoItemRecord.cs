namespace Application.Domain.Entities;

public record TodoItemRecord(
    Guid Id,
    string Title,
    string? Note,
    PriorityLevel Priority,
    DateTime? DueDate,
    bool Done
);

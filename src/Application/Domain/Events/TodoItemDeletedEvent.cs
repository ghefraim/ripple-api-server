using Application.Domain.Common;
using Application.Domain.Entities;

namespace Application.Domain.Events;

internal sealed class TodoItemDeletedEvent(TodoItem item) : BaseEvent
{
    public TodoItem Item { get; } = item;
}
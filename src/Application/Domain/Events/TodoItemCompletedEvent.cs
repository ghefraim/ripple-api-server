using Application.Domain.Common;
using Application.Domain.Entities;

namespace Application.Domain.Events;

internal sealed class TodoItemCompletedEvent(TodoItem item) : BaseEvent
{
    public TodoItem Item { get; } = item;
}
using Application.Domain.Common;
using Application.Domain.Events;

namespace Application.Domain.Entities;

public class TodoItem : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid ListId { get; set; }

    public string? Title { get; set; }

    public string? Note { get; set; }

    public PriorityLevel Priority { get; set; }

    public DateTime? Reminder { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? AssignedToId { get; set; }

    private bool _done;
    public bool Done
    {
        get => _done;
        set
        {
            if (value && _done == false)
            {
                AddDomainEvent(new TodoItemCompletedEvent(this));
            }

            _done = value;
        }
    }

    public TodoList List { get; set; } = null!;

    public ApplicationUser? AssignedTo { get; set; }
}

public enum PriorityLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

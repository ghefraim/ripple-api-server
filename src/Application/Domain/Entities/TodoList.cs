using Application.Domain.Common;

namespace Application.Domain.Entities;

public class TodoList : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string? Title { get; set; }

    public string Colour { get; set; } = "#FFFFFF";

    public IList<TodoItem> Items { get; private set; } = new List<TodoItem>();
}
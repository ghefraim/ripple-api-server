namespace Application.UnitTests.Common.Builders;

public class TodoListBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _organizationId = Guid.NewGuid();
    private string _title = "Test Todo List";
    private string _colour = "#FFFFFF";
    private DateTime _createdOn = DateTime.UtcNow;
    private string? _createdBy;
    private bool _isDeleted;
    private readonly List<TodoItem> _items = [];

    public TodoListBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TodoListBuilder WithOrganizationId(Guid organizationId)
    {
        _organizationId = organizationId;
        return this;
    }

    public TodoListBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TodoListBuilder WithColour(string colour)
    {
        _colour = colour;
        return this;
    }

    public TodoListBuilder WithCreatedOn(DateTime createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public TodoListBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public TodoListBuilder WithIsDeleted(bool isDeleted)
    {
        _isDeleted = isDeleted;
        return this;
    }

    public TodoListBuilder WithItem(TodoItem item)
    {
        _items.Add(item);
        return this;
    }

    public TodoListBuilder WithItems(IEnumerable<TodoItem> items)
    {
        _items.AddRange(items);
        return this;
    }

    public TodoList Build()
    {
        var list = new TodoList
        {
            Id = _id,
            OrganizationId = _organizationId,
            Title = _title,
            Colour = _colour,
            CreatedOn = _createdOn,
            CreatedBy = _createdBy,
            IsDeleted = _isDeleted,
        };

        foreach (var item in _items)
        {
            item.ListId = _id;
            item.OrganizationId = _organizationId;
        }

        return list;
    }

    public static TodoListBuilder Default() => new();
}

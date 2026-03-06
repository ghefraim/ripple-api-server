namespace Application.UnitTests.Common.Builders;

public class TodoItemBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _organizationId = Guid.NewGuid();
    private Guid _listId = Guid.NewGuid();
    private string _title = "Test Todo Item";
    private string? _note;
    private PriorityLevel _priority = PriorityLevel.None;
    private DateTime? _reminder;
    private DateTime? _dueDate;
    private Guid? _assignedToId;
    private bool _done;
    private DateTime _createdOn = DateTime.UtcNow;
    private string? _createdBy;
    private bool _isDeleted;

    public TodoItemBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TodoItemBuilder WithOrganizationId(Guid organizationId)
    {
        _organizationId = organizationId;
        return this;
    }

    public TodoItemBuilder WithListId(Guid listId)
    {
        _listId = listId;
        return this;
    }

    public TodoItemBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TodoItemBuilder WithNote(string? note)
    {
        _note = note;
        return this;
    }

    public TodoItemBuilder WithPriority(PriorityLevel priority)
    {
        _priority = priority;
        return this;
    }

    public TodoItemBuilder WithReminder(DateTime? reminder)
    {
        _reminder = reminder;
        return this;
    }

    public TodoItemBuilder WithDueDate(DateTime? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public TodoItemBuilder WithAssignedToId(Guid? assignedToId)
    {
        _assignedToId = assignedToId;
        return this;
    }

    public TodoItemBuilder WithDone(bool done)
    {
        _done = done;
        return this;
    }

    public TodoItemBuilder WithCreatedOn(DateTime createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public TodoItemBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public TodoItemBuilder WithIsDeleted(bool isDeleted)
    {
        _isDeleted = isDeleted;
        return this;
    }

    public TodoItem Build()
    {
        var item = new TodoItem
        {
            Id = _id,
            OrganizationId = _organizationId,
            ListId = _listId,
            Title = _title,
            Note = _note,
            Priority = _priority,
            Reminder = _reminder,
            DueDate = _dueDate,
            AssignedToId = _assignedToId,
            CreatedOn = _createdOn,
            CreatedBy = _createdBy,
            IsDeleted = _isDeleted,
        };

        // Note: Setting Done through reflection to avoid triggering domain event in builder
        // For tests that need the event, set Done after building
        var doneField = typeof(TodoItem).GetField("_done", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        doneField?.SetValue(item, _done);

        return item;
    }

    public static TodoItemBuilder Default() => new();
}

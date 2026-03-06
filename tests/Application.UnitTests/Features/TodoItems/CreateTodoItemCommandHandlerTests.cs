using Application.Common.Exceptions;
using Application.Features.TodoItems.CreateTodoItem;
using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Fixtures;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Features.TodoItems;

public class CreateTodoItemCommandHandlerTests : IDisposable
{
    private readonly Guid _organizationId;
    private readonly Guid _listId;
    private readonly ApplicationDbContext _context;
    private readonly CreateTodoItemCommandHandler _sut;

    public CreateTodoItemCommandHandlerTests()
    {
        _organizationId = Guid.NewGuid();
        _listId = Guid.NewGuid();

        var currentUserService = MockCurrentUserService.CreateOwner(_organizationId);
        _context = TestDbContextFactory.CreateWithOrganization(_organizationId);

        // Create a todo list for the tests
        var todoList = TodoListBuilder.Default()
            .WithId(_listId)
            .WithOrganizationId(_organizationId)
            .WithTitle("Test List")
            .Build();
        _context.TodoLists.Add(todoList);
        _context.SaveChanges();

        _sut = new CreateTodoItemCommandHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Success Cases

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateTodoItem()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Buy groceries",
            "Milk, eggs, bread",
            PriorityLevel.Medium,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Buy groceries");
        result.Note.Should().Be("Milk, eggs, bread");
        result.Priority.Should().Be(PriorityLevel.Medium);
        result.Done.Should().BeFalse();
        result.ListId.Should().Be(_listId);
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Test item",
            null,
            PriorityLevel.High,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var todoItem = await _context.TodoItems.FindAsync(result.Id);
        todoItem.Should().NotBeNull();
        todoItem!.Title.Should().Be("Test item");
        todoItem.Priority.Should().Be(PriorityLevel.High);
    }

    [Fact]
    public async Task Handle_ShouldCopyOrganizationIdFromList()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Test item",
            null,
            PriorityLevel.None,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var todoItem = await _context.TodoItems.FindAsync(result.Id);
        todoItem!.OrganizationId.Should().Be(_organizationId);
    }

    [Fact]
    public async Task Handle_WithAllFields_ShouldCreateItemWithAllValues()
    {
        // Arrange
        var reminder = DateTime.UtcNow.AddDays(1);
        var dueDate = DateTime.UtcNow.AddDays(7);

        var command = new CreateTodoItemCommand(
            _listId,
            "Complete task",
            "Important notes here",
            PriorityLevel.High,
            reminder,
            dueDate,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("Complete task");
        result.Note.Should().Be("Important notes here");
        result.Priority.Should().Be(PriorityLevel.High);
        result.Reminder.Should().BeCloseTo(reminder, TimeSpan.FromSeconds(1));
        result.DueDate.Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_WithAssignedUser_ShouldSetAssignedToId()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default()
            .WithEmail("assigned@example.com")
            .WithUserName("assigneduser")
            .Build();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var command = new CreateTodoItemCommand(
            _listId,
            "Assigned task",
            null,
            PriorityLevel.Medium,
            null,
            null,
            user.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.AssignedToId.Should().Be(user.Id);
        result.AssignedToName.Should().Be("assigneduser");
    }

    [Fact]
    public async Task Handle_ShouldReturnCreatedItem()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Test item",
            null,
            PriorityLevel.None,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert - CreatedOn is set by interceptors in integration tests
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.ListId.Should().Be(_listId);
    }

    [Fact]
    public async Task Handle_ShouldAddDomainEvent()
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Test item",
            null,
            PriorityLevel.None,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var todoItem = await _context.TodoItems.FindAsync(result.Id);
        todoItem!.DomainEvents.Should().ContainSingle();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Handle_WithNonExistentList_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentListId = Guid.NewGuid();
        var command = new CreateTodoItemCommand(
            nonExistentListId,
            "Test item",
            null,
            PriorityLevel.None,
            null,
            null,
            null);

        // Act
        var act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*TodoList*{nonExistentListId}*");
    }

    [Fact]
    public async Task Handle_WithNonExistentAssignedUser_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var command = new CreateTodoItemCommand(
            _listId,
            "Test item",
            null,
            PriorityLevel.None,
            null,
            null,
            nonExistentUserId);

        // Act
        var act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*User*{nonExistentUserId}*");
    }

    #endregion

    #region Priority Tests

    [Theory]
    [InlineData(PriorityLevel.None)]
    [InlineData(PriorityLevel.Low)]
    [InlineData(PriorityLevel.Medium)]
    [InlineData(PriorityLevel.High)]
    public async Task Handle_WithDifferentPriorityLevels_ShouldCreateWithCorrectPriority(PriorityLevel priority)
    {
        // Arrange
        var command = new CreateTodoItemCommand(
            _listId,
            "Priority test",
            null,
            priority,
            null,
            null,
            null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Priority.Should().Be(priority);
    }

    #endregion
}

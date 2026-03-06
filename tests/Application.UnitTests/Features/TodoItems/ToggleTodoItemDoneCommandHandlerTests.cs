using Application.Common.Exceptions;
using Application.Features.TodoItems.ToggleTodoItemDone;
using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Fixtures;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Features.TodoItems;

public class ToggleTodoItemDoneCommandHandlerTests : IDisposable
{
    private readonly Guid _organizationId;
    private readonly Guid _listId;
    private readonly ApplicationDbContext _context;
    private readonly ToggleTodoItemDoneCommandHandler _sut;

    public ToggleTodoItemDoneCommandHandlerTests()
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

        _sut = new ToggleTodoItemDoneCommandHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<TodoItem> CreateTodoItemAsync(bool done = false)
    {
        var item = TodoItemBuilder.Default()
            .WithOrganizationId(_organizationId)
            .WithListId(_listId)
            .WithTitle("Test Item")
            .WithDone(done)
            .Build();

        _context.TodoItems.Add(item);
        await _context.SaveChangesAsync();

        return item;
    }

    #region Toggle Tests

    [Fact]
    public async Task Handle_WithUndoneItem_ShouldMarkAsDone()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Done.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDoneItem_ShouldMarkAsUndone()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: true);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Done.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var updatedItem = await _context.TodoItems.FindAsync(item.Id);
        updatedItem!.Done.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnToggleResult()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert - UpdatedOn is set by interceptors in integration tests
        result.Should().NotBeNull();
        result.Id.Should().Be(item.Id);
        result.Done.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectResponse()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().Be(item.Id);
        result.ListId.Should().Be(_listId);
        result.Title.Should().Be("Test Item");
    }

    #endregion

    #region Domain Event Tests

    [Fact]
    public async Task Handle_WhenMarkingAsDone_ShouldTriggerDomainEvent()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var updatedItem = await _context.TodoItems.FindAsync(item.Id);
        updatedItem!.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenMarkingAsUndone_ShouldNotTriggerTodoItemCompletedEvent()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: true);
        item.ClearDomainEvents(); // Clear any events from initial setup
        await _context.SaveChangesAsync();

        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        var updatedItem = await _context.TodoItems.FindAsync(item.Id);
        updatedItem!.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Handle_WithNonExistentItem_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new ToggleTodoItemDoneCommand(nonExistentId);

        // Act
        var act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*TodoItem*{nonExistentId}*");
    }

    #endregion

    #region Multiple Toggle Tests

    [Fact]
    public async Task Handle_MultipleTimes_ShouldAlternateState()
    {
        // Arrange
        var item = await CreateTodoItemAsync(done: false);
        var command = new ToggleTodoItemDoneCommand(item.Id);

        // Act & Assert - First toggle (false -> true)
        var result1 = await _sut.Handle(command, CancellationToken.None);
        result1.Done.Should().BeTrue();

        // Second toggle (true -> false)
        var result2 = await _sut.Handle(command, CancellationToken.None);
        result2.Done.Should().BeFalse();

        // Third toggle (false -> true)
        var result3 = await _sut.Handle(command, CancellationToken.None);
        result3.Done.Should().BeTrue();
    }

    #endregion
}

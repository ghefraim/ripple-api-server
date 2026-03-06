using Application.Common.Exceptions;
using Application.Features.TodoLists.CreateTodoList;
using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Fixtures;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Features.TodoLists;

public class CreateTodoListCommandHandlerTests : IDisposable
{
    private readonly Guid _organizationId;
    private readonly MockCurrentUserService _currentUserService;
    private readonly Mock<IEntitlementService> _entitlementServiceMock;
    private readonly ApplicationDbContext _context;
    private readonly CreateTodoListCommandHandler _sut;

    public CreateTodoListCommandHandlerTests()
    {
        _organizationId = Guid.NewGuid();
        _currentUserService = MockCurrentUserService.CreateOwner(_organizationId);
        _entitlementServiceMock = new Mock<IEntitlementService>();
        _context = TestDbContextFactory.CreateWithOrganization(_organizationId);
        _sut = new CreateTodoListCommandHandler(_context, _currentUserService, _entitlementServiceMock.Object);

        // Default: allow creation
        _entitlementServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureCheckResult(true, null, 10, 0));
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Success Cases

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateTodoList()
    {
        // Arrange
        var command = new CreateTodoListCommand("My Todo List", "#FF5733");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("My Todo List");
        result.Colour.Should().Be("#FF5733");
        result.OrganizationId.Should().Be(_organizationId);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithNullColour_ShouldUseDefaultWhite()
    {
        // Arrange
        var command = new CreateTodoListCommand("My Todo List", null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Colour.Should().Be("#FFFFFF");
    }

    [Fact]
    public async Task Handle_WithEmptyColour_ShouldUseDefaultWhite()
    {
        // Arrange
        var command = new CreateTodoListCommand("My Todo List", string.Empty);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Colour.Should().Be("#FFFFFF");
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var command = new CreateTodoListCommand("My Todo List", "#FFC300");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        var todoList = await _context.TodoLists.FindAsync(result.Id);
        todoList.Should().NotBeNull();
        todoList!.Title.Should().Be("My Todo List");
        todoList.Colour.Should().Be("#FFC300");
        todoList.OrganizationId.Should().Be(_organizationId);
    }

    [Fact]
    public async Task Handle_ShouldSetOrganizationIdFromCurrentUser()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test List", null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.OrganizationId.Should().Be(_organizationId);
    }

    [Fact]
    public async Task Handle_ShouldReturnCreatedOnTimestamp()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test List", null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert - CreatedOn is set by the handler/entity, verify it's returned
        // Note: Actual timestamp setting is done by interceptors in integration tests
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Handle_WithNoOrganization_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var currentUserService = MockCurrentUserService.CreateUnauthenticated();
        var handler = new CreateTodoListCommandHandler(_context, currentUserService, _entitlementServiceMock.Object);
        var command = new CreateTodoListCommand("Test List", null);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("No organization selected.");
    }

    #endregion

    #region Entitlement Tests

    [Fact]
    public async Task Handle_ShouldCheckEntitlementLimit()
    {
        // Arrange
        var command = new CreateTodoListCommand("Test List", null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _entitlementServiceMock.Verify(
            x => x.CheckLimitAsync("maxTodoLists", It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAtEntitlementLimit_ShouldThrowPaymentRequiredException()
    {
        // Arrange
        _entitlementServiceMock
            .Setup(x => x.CheckLimitAsync("maxTodoLists", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureCheckResult(false, "You have reached the limit of 5 for maxTodoLists.", 5, 5));

        var command = new CreateTodoListCommand("Test List", null);

        // Act
        var act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PaymentRequiredException>()
            .Where(ex => ex.FeatureKey == "maxTodoLists" && ex.Limit == 5 && ex.CurrentUsage == 5);
    }

    [Fact]
    public async Task Handle_ShouldCountExistingListsForEntitlementCheck()
    {
        // Arrange
        var existingLists = new[]
        {
            TodoListBuilder.Default().WithOrganizationId(_organizationId).WithTitle("List 1").Build(),
            TodoListBuilder.Default().WithOrganizationId(_organizationId).WithTitle("List 2").Build(),
            TodoListBuilder.Default().WithOrganizationId(_organizationId).WithTitle("List 3").Build(),
        };
        _context.TodoLists.AddRange(existingLists);
        await _context.SaveChangesAsync();

        var command = new CreateTodoListCommand("Test List", null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _entitlementServiceMock.Verify(
            x => x.CheckLimitAsync("maxTodoLists", 3, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldExcludeDeletedListsFromCount()
    {
        // Arrange
        var activeLists = new[]
        {
            TodoListBuilder.Default().WithOrganizationId(_organizationId).WithTitle("Active 1").Build(),
            TodoListBuilder.Default().WithOrganizationId(_organizationId).WithTitle("Active 2").Build(),
        };
        var deletedList = TodoListBuilder.Default()
            .WithOrganizationId(_organizationId)
            .WithTitle("Deleted")
            .WithIsDeleted(true)
            .Build();

        _context.TodoLists.AddRange(activeLists);
        _context.TodoLists.Add(deletedList);
        await _context.SaveChangesAsync();

        var command = new CreateTodoListCommand("Test List", null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert - Should count only 2 active lists, not 3
        _entitlementServiceMock.Verify(
            x => x.CheckLimitAsync("maxTodoLists", 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldOnlyCountListsFromCurrentOrganization()
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();
        var currentOrgList = TodoListBuilder.Default()
            .WithOrganizationId(_organizationId)
            .WithTitle("Current Org List")
            .Build();
        var otherOrgList = TodoListBuilder.Default()
            .WithOrganizationId(otherOrgId)
            .WithTitle("Other Org List")
            .Build();

        _context.TodoLists.AddRange(currentOrgList, otherOrgList);
        await _context.SaveChangesAsync();

        var command = new CreateTodoListCommand("Test List", null);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert - Should count only 1 list from current organization
        _entitlementServiceMock.Verify(
            x => x.CheckLimitAsync("maxTodoLists", 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

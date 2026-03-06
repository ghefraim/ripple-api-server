using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Fixtures;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Persistence;

public class OrganizationScopingTests : IDisposable
{
    private readonly Guid _org1Id;
    private readonly Guid _org2Id;
    private readonly string _dbName;

    public OrganizationScopingTests()
    {
        _org1Id = Guid.NewGuid();
        _org2Id = Guid.NewGuid();
        _dbName = Guid.NewGuid().ToString();
    }

    public void Dispose()
    {
        // Cleanup is automatic with InMemory database
    }

    private ApplicationDbContext CreateContextForOrg(Guid organizationId)
    {
        return TestDbContextFactory.CreateWithOrganization(organizationId, databaseName: _dbName);
    }

    private ApplicationDbContext CreateContextWithoutOrg()
    {
        return TestDbContextFactory.CreateWithoutOrganization(databaseName: _dbName);
    }

    #region TodoList Scoping Tests

    [Fact]
    public async Task TodoLists_ShouldOnlyReturnCurrentOrganizationData()
    {
        // Arrange - Create lists for two different organizations
        var setupContext = CreateContextWithoutOrg();

        var org1List = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Org1 List")
            .Build();
        var org2List = TodoListBuilder.Default()
            .WithOrganizationId(_org2Id)
            .WithTitle("Org2 List")
            .Build();

        setupContext.TodoLists.AddRange(org1List, org2List);
        await setupContext.SaveChangesAsync();

        // Act - Query as Org1
        await using var org1Context = CreateContextForOrg(_org1Id);
        var org1Results = await org1Context.TodoLists.ToListAsync();

        // Act - Query as Org2
        await using var org2Context = CreateContextForOrg(_org2Id);
        var org2Results = await org2Context.TodoLists.ToListAsync();

        // Assert
        org1Results.Should().HaveCount(1);
        org1Results.Single().Title.Should().Be("Org1 List");

        org2Results.Should().HaveCount(1);
        org2Results.Single().Title.Should().Be("Org2 List");
    }

    [Fact]
    public async Task TodoLists_WithNoOrganizationSelected_ShouldReturnAllData()
    {
        // Arrange
        var setupContext = CreateContextWithoutOrg();

        var org1List = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Org1 List")
            .Build();
        var org2List = TodoListBuilder.Default()
            .WithOrganizationId(_org2Id)
            .WithTitle("Org2 List")
            .Build();

        setupContext.TodoLists.AddRange(org1List, org2List);
        await setupContext.SaveChangesAsync();

        // Act - Query without organization context
        await using var noOrgContext = CreateContextWithoutOrg();
        var results = await noOrgContext.TodoLists.ToListAsync();

        // Assert - Without org selected, query filters don't apply
        results.Should().HaveCount(2);
    }

    #endregion

    #region TodoItem Scoping Tests

    [Fact]
    public async Task TodoItems_ShouldOnlyReturnCurrentOrganizationData()
    {
        // Arrange
        var setupContext = CreateContextWithoutOrg();

        var list1 = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .Build();
        var list2 = TodoListBuilder.Default()
            .WithOrganizationId(_org2Id)
            .Build();

        var item1 = TodoItemBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithListId(list1.Id)
            .WithTitle("Org1 Item")
            .Build();
        var item2 = TodoItemBuilder.Default()
            .WithOrganizationId(_org2Id)
            .WithListId(list2.Id)
            .WithTitle("Org2 Item")
            .Build();

        setupContext.TodoLists.AddRange(list1, list2);
        setupContext.TodoItems.AddRange(item1, item2);
        await setupContext.SaveChangesAsync();

        // Act
        await using var org1Context = CreateContextForOrg(_org1Id);
        var org1Items = await org1Context.TodoItems.ToListAsync();

        await using var org2Context = CreateContextForOrg(_org2Id);
        var org2Items = await org2Context.TodoItems.ToListAsync();

        // Assert
        org1Items.Should().HaveCount(1);
        org1Items.Single().Title.Should().Be("Org1 Item");

        org2Items.Should().HaveCount(1);
        org2Items.Single().Title.Should().Be("Org2 Item");
    }

    #endregion

    #region Soft Delete Filter Tests

    [Fact]
    public async Task TodoLists_ShouldExcludeSoftDeletedRecords()
    {
        // Arrange
        var setupContext = CreateContextWithoutOrg();

        var activeList = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Active List")
            .WithIsDeleted(false)
            .Build();
        var deletedList = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Deleted List")
            .WithIsDeleted(true)
            .Build();

        setupContext.TodoLists.AddRange(activeList, deletedList);
        await setupContext.SaveChangesAsync();

        // Act
        await using var context = CreateContextForOrg(_org1Id);
        var results = await context.TodoLists.ToListAsync();

        // Assert
        results.Should().HaveCount(1);
        results.Single().Title.Should().Be("Active List");
    }

    [Fact]
    public async Task TodoLists_WithIgnoreQueryFilters_ShouldIncludeSoftDeletedRecords()
    {
        // Arrange
        var setupContext = CreateContextWithoutOrg();

        var activeList = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Active List")
            .WithIsDeleted(false)
            .Build();
        var deletedList = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Deleted List")
            .WithIsDeleted(true)
            .Build();

        setupContext.TodoLists.AddRange(activeList, deletedList);
        await setupContext.SaveChangesAsync();

        // Act
        await using var context = CreateContextForOrg(_org1Id);
        var results = await context.TodoLists.IgnoreQueryFilters().ToListAsync();

        // Assert - Should include soft-deleted records
        results.Should().HaveCount(2);
    }

    #endregion

    #region Data Isolation Tests

    [Fact]
    public async Task SwitchingOrganization_ShouldChangeVisibleData()
    {
        // Arrange
        var setupContext = CreateContextWithoutOrg();

        var org1List = TodoListBuilder.Default()
            .WithOrganizationId(_org1Id)
            .WithTitle("Org1 List")
            .Build();
        var org2List = TodoListBuilder.Default()
            .WithOrganizationId(_org2Id)
            .WithTitle("Org2 List")
            .Build();

        setupContext.TodoLists.AddRange(org1List, org2List);
        await setupContext.SaveChangesAsync();

        // Act - First as Org1
        await using var org1Context = CreateContextForOrg(_org1Id);
        var org1Count = await org1Context.TodoLists.CountAsync();

        // Act - Then as Org2
        await using var org2Context = CreateContextForOrg(_org2Id);
        var org2Count = await org2Context.TodoLists.CountAsync();

        // Assert
        org1Count.Should().Be(1);
        org2Count.Should().Be(1);
    }

    [Fact]
    public async Task CreateTodoList_ShouldBeVisibleOnlyToOwningOrganization()
    {
        // Arrange - Create a list as Org1
        await using var org1Context = CreateContextForOrg(_org1Id);
        var newList = new TodoList
        {
            OrganizationId = _org1Id,
            Title = "New Org1 List",
            Colour = "#FFFFFF",
        };
        org1Context.TodoLists.Add(newList);
        await org1Context.SaveChangesAsync();

        // Act - Try to read as Org2
        await using var org2Context = CreateContextForOrg(_org2Id);
        var org2Lists = await org2Context.TodoLists.ToListAsync();

        // Act - Read as Org1
        await using var org1ContextRead = CreateContextForOrg(_org1Id);
        var org1Lists = await org1ContextRead.TodoLists.ToListAsync();

        // Assert
        org2Lists.Should().BeEmpty();
        org1Lists.Should().HaveCount(1);
        org1Lists.Single().Title.Should().Be("New Org1 List");
    }

    #endregion
}

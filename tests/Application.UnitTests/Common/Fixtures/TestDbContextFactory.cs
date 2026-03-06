using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Common.Fixtures;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create(
        ICurrentUserService? currentUserService = null,
        IDateTime? dateTime = null,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(
            options,
            dateTime ?? MockDateTime.Create(),
            currentUserService ?? new MockCurrentUserService());

        context.Database.EnsureCreated();

        return context;
    }

    public static ApplicationDbContext CreateWithOrganization(
        Guid organizationId,
        OrganizationRole role = OrganizationRole.Owner,
        IDateTime? dateTime = null,
        string? databaseName = null)
    {
        var currentUserService = new MockCurrentUserService
        {
            OrganizationId = organizationId,
            OrganizationRole = role,
        };

        return Create(currentUserService, dateTime, databaseName);
    }

    public static ApplicationDbContext CreateWithoutOrganization(
        IDateTime? dateTime = null,
        string? databaseName = null)
    {
        var currentUserService = MockCurrentUserService.CreateUnauthenticated();
        return Create(currentUserService, dateTime, databaseName);
    }

    public static async Task<ApplicationDbContext> CreateSeededAsync(
        Guid organizationId,
        int todoListCount = 1,
        int itemsPerList = 3,
        OrganizationRole role = OrganizationRole.Owner,
        IDateTime? dateTime = null,
        string? databaseName = null)
    {
        var context = CreateWithOrganization(organizationId, role, dateTime, databaseName);

        var organization = new Organization
        {
            Id = organizationId,
            Name = "Test Organization",
            CreatedOn = DateTime.UtcNow,
        };
        context.Organizations.Add(organization);

        for (int i = 0; i < todoListCount; i++)
        {
            var list = new TodoList
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Title = $"Test List {i + 1}",
                Colour = "#FFFFFF",
                CreatedOn = DateTime.UtcNow,
            };
            context.TodoLists.Add(list);

            for (int j = 0; j < itemsPerList; j++)
            {
                var item = new TodoItem
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    ListId = list.Id,
                    Title = $"Test Item {j + 1}",
                    Priority = PriorityLevel.None,
                    CreatedOn = DateTime.UtcNow,
                };
                context.TodoItems.Add(item);
            }
        }

        await context.SaveChangesAsync();

        return context;
    }
}

using Application.Common.Models;
using Application.Infrastructure.Persistence;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Fixtures;
using Application.UnitTests.Common.Mocks;

namespace Application.UnitTests.Services;

public class EntitlementServiceTests : IDisposable
{
    private readonly Guid _organizationId;
    private readonly MockCurrentUserService _currentUserService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<EntitlementService>> _loggerMock;
    private readonly IOptions<BillingOptions> _billingOptions;
    private readonly ApplicationDbContext _context;
    private readonly EntitlementService _sut;

    public EntitlementServiceTests()
    {
        _organizationId = Guid.NewGuid();
        _currentUserService = MockCurrentUserService.CreateOwner(_organizationId);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<EntitlementService>>();
        _billingOptions = Options.Create(new BillingOptions { EntitlementCacheDurationMinutes = 5 });
        _context = TestDbContextFactory.CreateWithOrganization(_organizationId);
        _sut = new EntitlementService(_context, _currentUserService, _cache, _billingOptions, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    #region GetEntitlementsAsync Tests

    [Fact]
    public async Task GetEntitlementsAsync_WithNoEntitlements_ShouldReturnEmptyDictionary()
    {
        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitlementsAsync_WithEntitlements_ShouldReturnAllEntitlements()
    {
        // Arrange
        var entitlement1 = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        var entitlement2 = EntitlementBuilder.ForAccess("advancedFeature")
            .WithOrganizationId(_organizationId)
            .Build();

        _context.Entitlements.AddRange(entitlement1, entitlement2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().HaveCount(2);
        result["maxTodoLists"].Should().Be("10");
        result["advancedFeature"].Should().Be("true");
    }

    [Fact]
    public async Task GetEntitlementsAsync_WithExpiredEntitlement_ShouldExcludeExpired()
    {
        // Arrange
        var validEntitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithExpiresAt(DateTime.UtcNow.AddDays(1))
            .Build();
        var expiredEntitlement = EntitlementBuilder.ForLimit("maxItems", 100)
            .WithOrganizationId(_organizationId)
            .WithExpiresAt(DateTime.UtcNow.AddDays(-1))
            .Build();

        _context.Entitlements.AddRange(validEntitlement, expiredEntitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("maxTodoLists");
        result.Should().NotContainKey("maxItems");
    }

    [Fact]
    public async Task GetEntitlementsAsync_WithDeletedEntitlement_ShouldExcludeDeleted()
    {
        // Arrange
        var validEntitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        var deletedEntitlement = EntitlementBuilder.ForLimit("maxItems", 100)
            .WithOrganizationId(_organizationId)
            .WithIsDeleted(true)
            .Build();

        _context.Entitlements.AddRange(validEntitlement, deletedEntitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("maxTodoLists");
    }

    [Fact]
    public async Task GetEntitlementsAsync_WithMultipleLimitEntitlements_ShouldReturnMaxValue()
    {
        // Arrange
        var entitlement1 = EntitlementBuilder.ForLimit("maxTodoLists", 5)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .Build();
        var entitlement2 = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Addon)
            .Build();

        _context.Entitlements.AddRange(entitlement1, entitlement2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result["maxTodoLists"].Should().Be("10");
    }

    [Fact]
    public async Task GetEntitlementsAsync_WithUnlimitedAndLimitEntitlements_ShouldReturnUnlimited()
    {
        // Arrange
        var limitedEntitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .Build();
        var unlimitedEntitlement = EntitlementBuilder.Unlimited("maxTodoLists")
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Addon)
            .Build();

        _context.Entitlements.AddRange(limitedEntitlement, unlimitedEntitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result["maxTodoLists"].Should().Be("unlimited");
    }

    [Fact]
    public async Task GetEntitlementsAsync_ShouldCacheResults()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act - First call
        var result1 = await _sut.GetEntitlementsAsync(_organizationId);

        // Modify data directly (bypass service)
        entitlement.Value = "20";
        await _context.SaveChangesAsync();

        // Act - Second call (should return cached data)
        var result2 = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result1["maxTodoLists"].Should().Be("10");
        result2["maxTodoLists"].Should().Be("10"); // Still cached
    }

    [Fact]
    public async Task GetEntitlementsAsync_ForDifferentOrganization_ShouldReturnEmpty()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        var differentOrgId = Guid.NewGuid();

        // Act
        var result = await _sut.GetEntitlementsAsync(differentOrgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CanAccessAsync Tests

    [Fact]
    public async Task CanAccessAsync_WithTrueValue_ShouldReturnTrue()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForAccess("advancedFeature", true)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CanAccessAsync("advancedFeature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_WithValueOne_ShouldReturnTrue()
    {
        // Arrange
        var entitlement = EntitlementBuilder.Default()
            .WithOrganizationId(_organizationId)
            .WithFeatureKey("advancedFeature")
            .WithFeatureType("access")
            .WithValue("1")
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CanAccessAsync("advancedFeature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_WithUnlimitedValue_ShouldReturnTrue()
    {
        // Arrange
        var entitlement = EntitlementBuilder.Unlimited("advancedFeature")
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CanAccessAsync("advancedFeature");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_WithFalseValue_ShouldReturnFalse()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForAccess("advancedFeature", false)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CanAccessAsync("advancedFeature");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_WithNoEntitlement_ShouldReturnFalse()
    {
        // Act
        var result = await _sut.CanAccessAsync("nonExistentFeature");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_WithNoOrganization_ShouldReturnFalse()
    {
        // Arrange
        var currentUserService = MockCurrentUserService.CreateUnauthenticated();
        var sut = new EntitlementService(_context, currentUserService, _cache, _billingOptions, _loggerMock.Object);

        var entitlement = EntitlementBuilder.ForAccess("advancedFeature", true)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await sut.CanAccessAsync("advancedFeature");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetLimitAsync Tests

    [Fact]
    public async Task GetLimitAsync_WithNumericLimit_ShouldReturnLimit()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetLimitAsync("maxTodoLists");

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task GetLimitAsync_WithUnlimitedValue_ShouldReturnIntMaxValue()
    {
        // Arrange
        var entitlement = EntitlementBuilder.Unlimited("maxTodoLists")
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetLimitAsync("maxTodoLists");

        // Assert
        result.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task GetLimitAsync_WithNoEntitlement_ShouldReturnZero()
    {
        // Act
        var result = await _sut.GetLimitAsync("nonExistentFeature");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetLimitAsync_WithNoOrganization_ShouldReturnZero()
    {
        // Arrange
        var currentUserService = MockCurrentUserService.CreateUnauthenticated();
        var sut = new EntitlementService(_context, currentUserService, _cache, _billingOptions, _loggerMock.Object);

        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await sut.GetLimitAsync("maxTodoLists");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetLimitAsync_WithNonNumericValue_ShouldReturnZero()
    {
        // Arrange
        var entitlement = EntitlementBuilder.Default()
            .WithOrganizationId(_organizationId)
            .WithFeatureKey("maxTodoLists")
            .WithFeatureType("limit")
            .WithValue("not-a-number")
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetLimitAsync("maxTodoLists");

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region CheckLimitAsync Tests

    [Fact]
    public async Task CheckLimitAsync_WhenUnderLimit_ShouldReturnAllowed()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CheckLimitAsync("maxTodoLists", currentUsage: 5);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.Limit.Should().Be(10);
        result.CurrentUsage.Should().Be(5);
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task CheckLimitAsync_WhenAtLimit_ShouldReturnNotAllowed()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CheckLimitAsync("maxTodoLists", currentUsage: 10);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Limit.Should().Be(10);
        result.CurrentUsage.Should().Be(10);
        result.Message.Should().Contain("reached the limit");
    }

    [Fact]
    public async Task CheckLimitAsync_WhenOverLimit_ShouldReturnNotAllowed()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CheckLimitAsync("maxTodoLists", currentUsage: 15);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("reached the limit");
    }

    [Fact]
    public async Task CheckLimitAsync_WithUnlimited_ShouldAlwaysReturnAllowed()
    {
        // Arrange
        var entitlement = EntitlementBuilder.Unlimited("maxTodoLists")
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CheckLimitAsync("maxTodoLists", currentUsage: 1000000);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.Limit.Should().BeNull();
        result.CurrentUsage.Should().Be(1000000);
        result.Message.Should().BeNull();
    }

    [Fact]
    public async Task CheckLimitAsync_WithNoEntitlement_ShouldReturnNotAllowed()
    {
        // Act
        var result = await _sut.CheckLimitAsync("maxTodoLists", currentUsage: 1);

        // Assert
        result.IsAllowed.Should().BeFalse();
    }

    #endregion

    #region GrantAsync Tests

    [Fact]
    public async Task GrantAsync_ShouldCreateNewEntitlement()
    {
        // Act
        await _sut.GrantAsync(
            _organizationId,
            "maxTodoLists",
            "limit",
            "10",
            EntitlementSource.Plan);

        // Assert
        var entitlement = await _context.Entitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == _organizationId && e.FeatureKey == "maxTodoLists");

        entitlement.Should().NotBeNull();
        entitlement!.Value.Should().Be("10");
        entitlement.FeatureType.Should().Be("limit");
        entitlement.Source.Should().Be(EntitlementSource.Plan);
    }

    [Fact]
    public async Task GrantAsync_WithExistingEntitlement_ShouldUpdateValue()
    {
        // Arrange
        var existing = EntitlementBuilder.ForLimit("maxTodoLists", 5)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .Build();
        _context.Entitlements.Add(existing);
        await _context.SaveChangesAsync();

        // Act
        await _sut.GrantAsync(
            _organizationId,
            "maxTodoLists",
            "limit",
            "10",
            EntitlementSource.Plan);

        // Assert
        var entitlements = await _context.Entitlements
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == _organizationId && e.FeatureKey == "maxTodoLists")
            .ToListAsync();

        entitlements.Should().HaveCount(1);
        entitlements[0].Value.Should().Be("10");
    }

    [Fact]
    public async Task GrantAsync_WithManualSource_ShouldSetGrantedBy()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        _currentUserService.UserId = userId;

        // Act
        await _sut.GrantAsync(
            _organizationId,
            "maxTodoLists",
            "limit",
            "10",
            EntitlementSource.Manual,
            reason: "Manual override");

        // Assert
        var entitlement = await _context.Entitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == _organizationId && e.FeatureKey == "maxTodoLists");

        entitlement.Should().NotBeNull();
        entitlement!.GrantedBy.Should().Be(userId);
        entitlement.Reason.Should().Be("Manual override");
    }

    [Fact]
    public async Task GrantAsync_ShouldInvalidateCache()
    {
        // Arrange - Pre-populate cache
        var initialEntitlement = EntitlementBuilder.ForLimit("maxTodoLists", 5)
            .WithOrganizationId(_organizationId)
            .Build();
        _context.Entitlements.Add(initialEntitlement);
        await _context.SaveChangesAsync();
        await _sut.GetEntitlementsAsync(_organizationId); // Populate cache

        // Act
        await _sut.GrantAsync(
            _organizationId,
            "maxItems",
            "limit",
            "100",
            EntitlementSource.Plan);

        // Get entitlements again - should reflect new entitlement
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().ContainKey("maxItems");
        result["maxItems"].Should().Be("100");
    }

    #endregion

    #region RevokeAsync Tests

    [Fact]
    public async Task RevokeAsync_ShouldSoftDeleteEntitlement()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();

        // Act
        await _sut.RevokeAsync(_organizationId, "maxTodoLists", EntitlementSource.Plan);

        // Assert
        var deletedEntitlement = await _context.Entitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entitlement.Id);

        deletedEntitlement.Should().NotBeNull();
        deletedEntitlement!.IsDeleted.Should().BeTrue();
        deletedEntitlement.DeletedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAsync_WithNonExistentEntitlement_ShouldNotThrow()
    {
        // Act
        var act = async () => await _sut.RevokeAsync(_organizationId, "nonExistent", EntitlementSource.Plan);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAsync_ShouldInvalidateCache()
    {
        // Arrange
        var entitlement = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .Build();
        _context.Entitlements.Add(entitlement);
        await _context.SaveChangesAsync();
        await _sut.GetEntitlementsAsync(_organizationId); // Populate cache

        // Act
        await _sut.RevokeAsync(_organizationId, "maxTodoLists", EntitlementSource.Plan);
        var result = await _sut.GetEntitlementsAsync(_organizationId);

        // Assert
        result.Should().NotContainKey("maxTodoLists");
    }

    #endregion

    #region RevokeAllFromSourceAsync Tests

    [Fact]
    public async Task RevokeAllFromSourceAsync_ShouldSoftDeleteAllMatchingEntitlements()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var entitlement1 = EntitlementBuilder.ForLimit("maxTodoLists", 10)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .WithSourceId(sourceId)
            .Build();
        var entitlement2 = EntitlementBuilder.ForLimit("maxItems", 100)
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Plan)
            .WithSourceId(sourceId)
            .Build();
        var entitlement3 = EntitlementBuilder.ForAccess("premiumFeature")
            .WithOrganizationId(_organizationId)
            .WithSource(EntitlementSource.Addon) // Different source
            .Build();

        _context.Entitlements.AddRange(entitlement1, entitlement2, entitlement3);
        await _context.SaveChangesAsync();

        // Act
        await _sut.RevokeAllFromSourceAsync(_organizationId, EntitlementSource.Plan, sourceId);

        // Assert
        var entitlements = await _context.Entitlements
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == _organizationId)
            .ToListAsync();

        entitlements.Should().HaveCount(3);
        entitlements.Where(e => e.Source == EntitlementSource.Plan).All(e => e.IsDeleted).Should().BeTrue();
        entitlements.First(e => e.Source == EntitlementSource.Addon).IsDeleted.Should().BeFalse();
    }

    #endregion
}

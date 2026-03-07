using System.Linq.Expressions;
using System.Reflection;

using Application.Common.Interfaces;
using Application.Domain.Common;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence.Configurations;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Application.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly IDateTime _dateTime;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTime dateTime,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _dateTime = dateTime;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the current organization ID from the authenticated user.
    /// Used by EF Core query filters for automatic data isolation.
    /// </summary>
    private Guid? CurrentOrganizationId => _currentUserService.OrganizationId;

    public DbSet<AuditEntry> AuditLogs => Set<AuditEntry>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<BillingCustomer> BillingCustomers => Set<BillingCustomer>();
    public DbSet<StripeEventLog> StripeEventLogs => Set<StripeEventLog>();

    public DbSet<Gate> Gates => Set<Gate>();
    public DbSet<GroundCrew> GroundCrews => Set<GroundCrew>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Disruption> Disruptions => Set<Disruption>();
    public DbSet<CascadeImpact> CascadeImpacts => Set<CascadeImpact>();
    public DbSet<ActionPlan> ActionPlans => Set<ActionPlan>();
    public DbSet<OperationalRule> OperationalRules => Set<OperationalRule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AirportConfig> AirportConfigs => Set<AirportConfig>();
    public DbSet<CrewContact> CrewContacts => Set<CrewContact>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteTransactionAsync(Func<Task> action, CancellationToken token = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(token);
        try
        {
            await action();
            await SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(ApplicationDbContext))!);

        // Configure all DateTime properties to use UTC
        // This ensures PostgreSQL timestamp with time zone compatibility
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime())
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }

        // Ignore DomainEvents property on all entities inheriting from BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(BaseEntity.DomainEvents));
            }
        }

        // Configure query filters for soft delete and organization scoping
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            var isOrganizationScoped = typeof(IOrganizationScoped).IsAssignableFrom(clrType);

            if (!isSoftDeletable && !isOrganizationScoped)
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? filterExpression = null;

            // Soft delete filter: e.IsDeleted == false
            if (isSoftDeletable)
            {
                var propertyMethodInfo = typeof(EF).GetMethod("Property")!.MakeGenericMethod(typeof(bool));
                var isDeletedProperty = Expression.Call(propertyMethodInfo, parameter, Expression.Constant("IsDeleted"));
                var softDeleteFilter = Expression.MakeBinary(ExpressionType.Equal, isDeletedProperty, Expression.Constant(false));
                filterExpression = softDeleteFilter;
            }

            // Organization scope filter: e.OrganizationId == CurrentOrganizationId
            // Only applied when user has an organization selected (CurrentOrganizationId != null)
            if (isOrganizationScoped)
            {
                var organizationIdProperty = Expression.Property(parameter, nameof(IOrganizationScoped.OrganizationId));
                var currentOrgIdProperty = Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId));
                var hasOrgSelected = Expression.NotEqual(currentOrgIdProperty, Expression.Constant(null, typeof(Guid?)));
                var orgMatches = Expression.Equal(
                    Expression.Convert(organizationIdProperty, typeof(Guid?)),
                    currentOrgIdProperty);
                var orgFilter = Expression.OrElse(
                    Expression.Not(hasOrgSelected),
                    orgMatches);

                filterExpression = filterExpression != null
                    ? Expression.AndAlso(filterExpression, orgFilter)
                    : orgFilter;
            }

            if (filterExpression != null)
            {
                var lambda = Expression.Lambda(filterExpression, parameter);
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }
}

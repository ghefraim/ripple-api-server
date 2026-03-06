# Converting to Single-Tenant Mode (Backend)

This guide provides step-by-step instructions for converting the .NET backend from multi-tenant (organization-scoped) to single-tenant (user-scoped) mode.

**Important**: After conversion, data will be scoped by `CreatedBy` (user ID) via the `AuditableEntity` base class instead of `OrganizationId`.

---

## Table of Contents

1. [Files to DELETE](#1-files-to-delete)
2. [Files to MODIFY](#2-files-to-modify)
3. [Database Migration](#3-database-migration)
4. [Verification Checklist](#4-verification-checklist)

---

## 1. Files to DELETE

Remove these files entirely:

| File Path | Reason |
|-----------|--------|
| `src/Application/Domain/Entities/Organization.cs` | Organization entity no longer needed |
| `src/Application/Domain/Entities/UserOrganization.cs` | Organization membership no longer needed |
| `src/Application/Domain/Enums/OrganizationRole.cs` | Owner/Member roles no longer needed |
| `src/Application/Domain/Constants/CustomClaimTypes.cs` | Organization claim types no longer needed |
| `src/Application/Domain/Common/IOrganizationScoped.cs` | Organization scoping interface no longer needed |
| `src/Application/Infrastructure/Services/OrganizationService.cs` | Organization management no longer needed |
| `src/Application/Common/Interfaces/IOrganizationService.cs` | Organization service interface no longer needed |
| `src/Application/Common/Models/Organization/` | Delete entire directory (all org models) |
| `src/Application/Infrastructure/Persistence/Configurations/UserOrganizationConfiguration.cs` | Configuration no longer needed |
| `src/Application/Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs` | Configuration no longer needed |

---

## 2. Files to MODIFY

### 2.1 Entity Changes

Remove `IOrganizationScoped` interface and `OrganizationId` property from these entities:

#### `src/Application/Domain/Entities/TodoList.cs`

**Before:**
```csharp
public class TodoList : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    // ...
}
```

**After:**
```csharp
public class TodoList : AuditableEntity
{
    // OrganizationId removed - data scoped via CreatedBy (user ID)
    public string Title { get; set; } = string.Empty;
    // ...
}
```

#### `src/Application/Domain/Entities/TodoItem.cs`

**Before:**
```csharp
public class TodoItem : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid ListId { get; set; }
    // ...
}
```

**After:**
```csharp
public class TodoItem : AuditableEntity
{
    // OrganizationId removed - data scoped via CreatedBy (user ID)
    public Guid ListId { get; set; }
    // ...
}
```

#### `src/Application/Domain/Entities/ApiKey.cs`

**Before:**
```csharp
public class ApiKey : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    // ...
}
```

**After:**
```csharp
public class ApiKey : AuditableEntity
{
    // OrganizationId removed - data scoped via CreatedBy (user ID)
    public string UserId { get; set; } = string.Empty;
    // ...
}
```

#### `src/Application/Domain/Entities/AuditEntry.cs`

**Before:**
```csharp
public class AuditEntry : BaseEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    // ...
}
```

**After:**
```csharp
public class AuditEntry : BaseEntity
{
    // OrganizationId removed - data scoped via UserId property
    public string EntityName { get; set; } = string.Empty;
    // ...
}
```

#### `src/Application/Domain/Entities/Entitlement.cs` (if exists)

Remove `IOrganizationScoped` and `OrganizationId` property.

#### `src/Application/Domain/Entities/Subscription.cs` (if exists)

Remove `IOrganizationScoped` and `OrganizationId` property.

---

### 2.2 ApplicationDbContext

**File:** `src/Application/Infrastructure/Persistence/ApplicationDbContext.cs`

#### Remove DbSets

Delete these lines (around lines 43-44):
```csharp
public DbSet<Organization> Organizations => Set<Organization>();
public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
```

#### Remove CurrentOrganizationId property

Delete this line (around line 36):
```csharp
private Guid? CurrentOrganizationId => _currentUserService.OrganizationId;
```

#### Remove Organization Query Filter

In the `OnModelCreating` method, remove the entire organization scoping filter logic.

**Before** (simplified):
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    // ... other code ...

    foreach (var entityType in builder.Model.GetEntityTypes())
    {
        var isOrganizationScoped = typeof(IOrganizationScoped).IsAssignableFrom(entityType.ClrType);
        var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

        if (isSoftDeletable || isOrganizationScoped)
        {
            // Build filter expression
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression? filterExpression = null;

            // Soft delete filter
            if (isSoftDeletable)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                filterExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            }

            // Organization scope filter - DELETE THIS ENTIRE SECTION
            if (isOrganizationScoped)
            {
                var orgIdProperty = Expression.Property(parameter, nameof(IOrganizationScoped.OrganizationId));
                var currentOrgIdMethod = typeof(ApplicationDbContext).GetProperty(nameof(CurrentOrganizationId),
                    BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!;
                var currentOrgIdCall = Expression.Call(Expression.Constant(this), currentOrgIdMethod);
                var nullCheck = Expression.NotEqual(currentOrgIdCall, Expression.Constant(null, typeof(Guid?)));
                var orgIdEquals = Expression.Equal(orgIdProperty, Expression.Convert(currentOrgIdCall, typeof(Guid)));
                var orgFilter = Expression.OrElse(Expression.Not(nullCheck), orgIdEquals);

                filterExpression = filterExpression == null
                    ? orgFilter
                    : Expression.AndAlso(filterExpression, orgFilter);
            }

            // Apply the filter
            // ...
        }
    }
}
```

**After** (keep only soft-delete filter):
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    // ... other code ...

    foreach (var entityType in builder.Model.GetEntityTypes())
    {
        var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

        if (isSoftDeletable)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filterExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));

            var lambda = Expression.Lambda(filterExpression, parameter);
            builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
```

Also remove the `IOrganizationScoped` using statement if no longer needed.

---

### 2.3 ICurrentUserService

**File:** `src/Application/Common/Interfaces/ICurrentUserService.cs`

**Before:**
```csharp
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserEmail { get; }
    string? Role { get; }
    string? DeviceInfo { get; }
    bool IsApiRequest { get; }
    Guid? OrganizationId { get; }
    OrganizationRole? OrganizationRole { get; }
}
```

**After:**
```csharp
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserEmail { get; }
    string? Role { get; }
    string? DeviceInfo { get; }
    bool IsApiRequest { get; }
}
```

---

### 2.4 CurrentUserService

**File:** `src/Application/Infrastructure/Services/CurrentUserService.cs`

Remove these properties and their backing implementations:

**Delete properties:**
```csharp
public Guid? OrganizationId => GetOrganizationId();
public OrganizationRole? OrganizationRole => GetOrganizationRole();
```

**Delete methods:**
```csharp
private Guid? GetOrganizationId()
{
    var orgIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(CustomClaimTypes.OrganizationId);
    if (orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out var orgId))
    {
        return orgId;
    }
    return null;
}

private OrganizationRole? GetOrganizationRole()
{
    var roleClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(CustomClaimTypes.OrganizationRole);
    if (roleClaim != null && Enum.TryParse<OrganizationRole>(roleClaim.Value, out var role))
    {
        return role;
    }
    return null;
}
```

**Remove using statements:**
```csharp
using Application.Domain.Constants;
using Application.Domain.Enums;
```

---

### 2.5 ITokenService

**File:** `src/Application/Common/Interfaces/ITokenService.cs`

**Before:**
```csharp
public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles, Guid? organizationId, OrganizationRole? organizationRole);
    RefreshToken GenerateRefreshToken(string? deviceInfo = null);
    ClaimsPrincipal? ValidateToken(string token);
    DateTime GetRefreshTokenExpiry();
    Guid? ExtractOrganizationIdFromToken(string token);
}
```

**After:**
```csharp
public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    RefreshToken GenerateRefreshToken(string? deviceInfo = null);
    ClaimsPrincipal? ValidateToken(string token);
    DateTime GetRefreshTokenExpiry();
}
```

---

### 2.6 TokenService

**File:** `src/Application/Infrastructure/Services/TokenService.cs`

#### Update GenerateAccessToken method

**Before:**
```csharp
public string GenerateAccessToken(ApplicationUser user, IList<string> roles, Guid? organizationId, OrganizationRole? organizationRole)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Email, user.Email ?? string.Empty),
        new(ClaimTypes.Name, user.UserName ?? string.Empty),
    };

    // Add role claims
    foreach (var role in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    // Add organization claims if present
    if (organizationId.HasValue)
    {
        claims.Add(new Claim(CustomClaimTypes.OrganizationId, organizationId.Value.ToString()));
    }

    if (organizationRole.HasValue)
    {
        claims.Add(new Claim(CustomClaimTypes.OrganizationRole, organizationRole.Value.ToString()));
    }

    // ... rest of token generation
}
```

**After:**
```csharp
public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Email, user.Email ?? string.Empty),
        new(ClaimTypes.Name, user.UserName ?? string.Empty),
    };

    // Add role claims
    foreach (var role in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    // ... rest of token generation (unchanged)
}
```

#### Delete ExtractOrganizationIdFromToken method

Remove entire method:
```csharp
public Guid? ExtractOrganizationIdFromToken(string token)
{
    // ... entire method
}
```

#### Remove using statements

```csharp
using Application.Domain.Constants;
using Application.Domain.Enums;
```

---

### 2.7 AuthService

**File:** `src/Application/Infrastructure/Services/AuthService.cs`

#### Remove IOrganizationService dependency

**Before:**
```csharp
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IOrganizationService _organizationService;
    // ... other fields

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IOrganizationService organizationService,
        // ... other parameters
    )
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _organizationService = organizationService;
        // ...
    }
}
```

**After:**
```csharp
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    // ... other fields (remove _organizationService)

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        // ... other parameters (remove IOrganizationService)
    )
    {
        _userManager = userManager;
        _tokenService = tokenService;
        // ... (remove _organizationService assignment)
    }
}
```

#### Update Login/Register methods

In `LoginAsync`, `RegisterAsync`, `GoogleCallbackAsync`, etc., update token generation:

**Before:**
```csharp
var userWithOrgs = await _organizationService.GetUserWithOrganizationsAsync(user.Id);
var selectedOrg = userWithOrgs.UserOrganizations.FirstOrDefault(uo => uo.IsSelected);

var accessToken = _tokenService.GenerateAccessToken(
    user,
    roles,
    selectedOrg?.OrganizationId,
    selectedOrg?.Role
);
```

**After:**
```csharp
var accessToken = _tokenService.GenerateAccessToken(user, roles);
```

#### Update RefreshTokenAsync method

Remove organization ID extraction and preservation:

**Before:**
```csharp
public async Task<AuthenticationResponse> RefreshTokenAsync(string accessToken, string refreshToken)
{
    // ... validation code ...

    var organizationId = _tokenService.ExtractOrganizationIdFromToken(accessToken);
    var orgRole = /* get org role from old token */;

    var newAccessToken = _tokenService.GenerateAccessToken(user, roles, organizationId, orgRole);
    // ...
}
```

**After:**
```csharp
public async Task<AuthenticationResponse> RefreshTokenAsync(string accessToken, string refreshToken)
{
    // ... validation code ...

    var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
    // ...
}
```

#### Remove calls to organization service methods

Delete all calls to:
- `_organizationService.GetUserWithOrganizationsAsync()`
- `_organizationService.EnsureUserHasDefaultOrganizationAsync()`
- `_organizationService.SelectOrganizationAsync()`
- `_organizationService.CreateOrganizationAsync()`

#### Simplify BuildUserProfileResponseAsync

Remove organization-related response building:

**Before:**
```csharp
private async Task<UserProfileResponse> BuildUserProfileResponseAsync(ApplicationUser user)
{
    var userWithOrgs = await _organizationService.GetUserWithOrganizationsAsync(user.Id);

    return new UserProfileResponse
    {
        Id = user.Id,
        Email = user.Email,
        // ... other fields ...
        Organizations = userWithOrgs.UserOrganizations.Select(uo => new OrganizationMembership
        {
            OrganizationId = uo.OrganizationId,
            OrganizationName = uo.Organization.Name,
            Role = uo.Role,
            IsSelected = uo.IsSelected
        }).ToList(),
        SelectedOrganization = /* ... */
    };
}
```

**After:**
```csharp
private UserProfileResponse BuildUserProfileResponse(ApplicationUser user)
{
    return new UserProfileResponse
    {
        Id = user.Id,
        Email = user.Email,
        // ... other fields (remove Organizations, SelectedOrganization) ...
    };
}
```

---

### 2.8 AuthorizationBehaviour

**File:** `src/Application/Common/Behaviours/AuthorizationBehaviour.cs`

Remove organization role checks entirely.

**Before:**
```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
{
    // ... authentication checks ...

    // Check organization role requirements - DELETE THIS SECTION
    var requiredRoles = authorizeAttributes
        .Where(a => !string.IsNullOrEmpty(a.Roles))
        .SelectMany(a => a.Roles!.Split(','))
        .Select(r => r.Trim())
        .Distinct()
        .ToList();

    if (requiredRoles.Count > 0)
    {
        var currentRole = _currentUserService.OrganizationRole;

        if (currentRole == null)
        {
            throw new ForbiddenAccessException("No organization selected.");
        }

        var hasRequiredRole = requiredRoles.Any(role =>
            Enum.TryParse<OrganizationRole>(role, out var requiredRole) &&
            currentRole >= requiredRole);

        if (!hasRequiredRole)
        {
            throw new ForbiddenAccessException($"Required role: {string.Join(" or ", requiredRoles)}");
        }
    }

    return await next();
}
```

**After:**
```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
{
    // ... authentication checks only ...

    // Organization role checks removed - single-tenant mode

    return await next();
}
```

**Remove using statements:**
```csharp
using Application.Domain.Enums;
```

---

### 2.9 Command Handlers

#### CreateTodoListCommand.cs

**File:** `src/Application/Features/TodoLists/Commands/CreateTodoListCommand.cs`

**Changes:**
1. Change `[Authorize(Roles = "Owner")]` to `[Authorize]`
2. Remove `OrganizationId` assignment
3. Remove `OrganizationId` from response DTO

**Before:**
```csharp
[Authorize(Roles = "Owner")]
public record CreateTodoListCommand : IRequest<CreateTodoListResponse>
{
    public string Title { get; init; } = string.Empty;
    // ...
}

public class CreateTodoListCommandHandler : IRequestHandler<CreateTodoListCommand, CreateTodoListResponse>
{
    public async Task<CreateTodoListResponse> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        // ... entitlement check ...

        var entity = new TodoList
        {
            Title = request.Title,
            OrganizationId = _currentUserService.OrganizationId!.Value,
            // ...
        };

        // ...

        return new CreateTodoListResponse
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            // ...
        };
    }
}

public record CreateTodoListResponse
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    // ...
}
```

**After:**
```csharp
[Authorize]
public record CreateTodoListCommand : IRequest<CreateTodoListResponse>
{
    public string Title { get; init; } = string.Empty;
    // ...
}

public class CreateTodoListCommandHandler : IRequestHandler<CreateTodoListCommand, CreateTodoListResponse>
{
    public async Task<CreateTodoListResponse> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        // ... entitlement check (modify to check by user instead of org) ...

        var entity = new TodoList
        {
            Title = request.Title,
            // OrganizationId removed - CreatedBy set automatically by SaveChangesInterceptor
            // ...
        };

        // ...

        return new CreateTodoListResponse
        {
            Id = entity.Id,
            // OrganizationId removed
            // ...
        };
    }
}

public record CreateTodoListResponse
{
    public Guid Id { get; init; }
    // OrganizationId removed
    // ...
}
```

#### CreateTodoItemCommand.cs

**File:** `src/Application/Features/TodoItems/Commands/CreateTodoItemCommand.cs`

**Changes:**
1. Remove `OrganizationId` copy from parent list

**Before:**
```csharp
var entity = new TodoItem
{
    Title = request.Title,
    ListId = request.ListId,
    OrganizationId = list.OrganizationId, // Copy from parent
    // ...
};
```

**After:**
```csharp
var entity = new TodoItem
{
    Title = request.Title,
    ListId = request.ListId,
    // OrganizationId removed - CreatedBy set automatically
    // ...
};
```

#### CreateApiKeyCommand.cs

**File:** `src/Application/Features/ApiKeys/Commands/CreateApiKeyCommand.cs`

Remove `OrganizationId` assignment from the entity creation.

---

### 2.10 Response DTOs

Remove organization fields from these DTOs:

#### AuthenticationResponse

**File:** `src/Application/Common/Models/Auth/AuthenticationResponse.cs`

**Remove:**
```csharp
public List<OrganizationMembership>? Organizations { get; set; }
public OrganizationMembership? SelectedOrganization { get; set; }
```

#### UserProfileResponse

**File:** `src/Application/Common/Models/Auth/UserProfileResponse.cs`

**Remove:**
```csharp
public List<OrganizationMembership>? Organizations { get; set; }
public OrganizationMembership? SelectedOrganization { get; set; }
```

#### GoogleAuthenticationResponse (if separate)

Remove organization fields similarly.

---

### 2.11 AuthController

**File:** `src/Api/Controllers/AuthController.cs`

**Delete these endpoints:**
```csharp
[HttpPost("select-organization")]
public async Task<IActionResult> SelectOrganization([FromBody] SelectOrganizationRequest request)
{
    // ... entire method
}

[HttpPost("create-organization")]
public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request)
{
    // ... entire method
}
```

**Delete associated request DTOs if defined locally.**

---

### 2.12 InfrastructureServiceConfiguration

**File:** `src/Application/Infrastructure/InfrastructureServiceConfiguration.cs`

**Remove:**
```csharp
services.AddScoped<IOrganizationService, OrganizationService>();
```

---

### 2.13 Query Handlers (Optional - Add User Scoping)

If you want explicit user-scoped queries instead of relying on `CreatedBy`, update query handlers:

#### GetTodoListsQuery.cs

**File:** `src/Application/Features/TodoLists/Queries/GetTodoListsQuery.cs`

**Before:**
```csharp
// Relies on IOrganizationScoped filter
var lists = await _context.TodoLists.ToListAsync(cancellationToken);
```

**After:**
```csharp
// Explicit user scope
var userId = _currentUserService.UserId;
var lists = await _context.TodoLists
    .Where(l => l.CreatedBy == userId)
    .ToListAsync(cancellationToken);
```

> Note: This step is optional since `AuditableEntity.CreatedBy` already tracks the owner. You may prefer to add a global query filter by `CreatedBy` similar to the old `IOrganizationScoped` filter.

---

## 3. Database Migration

### Create the Migration

```bash
cd net-boilerplate

# Create migration to remove organization tables and columns
dotnet ef migrations add RemoveMultiTenancy --project src/Application --startup-project src/Api
```

Or using the Makefile:
```bash
make migration name=RemoveMultiTenancy
```

### Migration Should Handle

The migration will automatically:
1. Drop `UserOrganizations` table
2. Drop `Organizations` table
3. Drop `OrganizationId` column from:
   - `TodoLists`
   - `TodoItems`
   - `ApiKeys`
   - `AuditEntries`
   - `Entitlements` (if exists)
   - `Subscriptions` (if exists)
4. Drop related indexes and foreign key constraints

### Apply the Migration

```bash
dotnet ef database update --project src/Application --startup-project src/Api
```

Or:
```bash
make migrate
```

### Handle Existing Data

**Important**: Before running the migration, consider how to handle existing data:

1. **Option A: Fresh start** - If acceptable, drop and recreate the database
2. **Option B: Data preservation** - The migration will drop `OrganizationId` columns but data remains linked via `CreatedBy`

For Option B, existing records with `CreatedBy` populated will continue to be scoped to their creator.

---

## 4. Verification Checklist

After completing all modifications:

### Build Verification
```bash
dotnet build
```
- [ ] Build succeeds with no errors
- [ ] No warnings related to removed types

### Code Verification
```bash
# Search for any remaining organization references
grep -r "IOrganizationScoped" src/
grep -r "OrganizationId" src/
grep -r "OrganizationRole" src/
grep -r "IOrganizationService" src/
```
- [ ] No references to `IOrganizationScoped`
- [ ] No references to `OrganizationId` (except in migration files)
- [ ] No references to `OrganizationRole`
- [ ] No references to `IOrganizationService`

### Runtime Verification
- [ ] User registration works
- [ ] User login works (no org selection required)
- [ ] JWT tokens don't contain organization claims
- [ ] CRUD operations on TodoLists work
- [ ] CRUD operations on TodoItems work
- [ ] User A cannot see User B's data (via `CreatedBy` scoping)
- [ ] Token refresh works correctly

### API Endpoints
- [ ] `POST /api/auth/login` returns response without organization fields
- [ ] `POST /api/auth/register` creates user without default organization
- [ ] `POST /api/auth/select-organization` endpoint removed (returns 404)
- [ ] `POST /api/auth/create-organization` endpoint removed (returns 404)

---

## Summary of Removed Concepts

| Concept | Replacement |
|---------|-------------|
| `IOrganizationScoped` interface | Data scoped via `CreatedBy` from `AuditableEntity` |
| `OrganizationId` on entities | `CreatedBy` (user ID) from `AuditableEntity` |
| `Organization` entity | N/A - users own their data directly |
| `UserOrganization` membership | N/A - no shared access model |
| `OrganizationRole` (Owner/Member) | Standard `[Authorize]` attribute |
| Organization selection flow | Direct login to dashboard |
| Organization claims in JWT | Standard user claims only |

---

## Rollback Plan

If you need to revert:

1. Restore deleted files from version control
2. Revert modifications using `git checkout`
3. Create a rollback migration: `dotnet ef migrations add RestoreMultiTenancy`
4. Apply: `dotnet ef database update`

Keep a backup of your database before applying the removal migration.

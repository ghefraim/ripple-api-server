# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture Overview

This .NET 8 application uses **Vertical Slice Architecture** organized by features rather than technical layers. The codebase follows CQRS patterns with MediatR, Entity Framework Core, and implements comprehensive auditing and soft-delete functionality.

### Project Structure

- **Api**: ASP.NET Web API entry point with minimal controllers in `Controllers/`
- **Application**: Contains all business logic organized in vertical slices under `Features/`

### Key Architectural Patterns

**Domain Layer (`Application/Domain/`):**
- `BaseEntity`: Foundation class with domain events support
- `AuditableEntity`: Extends BaseEntity with audit metadata (CreatedOn/By, UpdatedOn/By) and soft delete (IsDeleted, DeletedOnUtc)
- `ISoftDeletable`: Interface for soft-delete capability
- All entities inherit from these base classes for consistent behavior

**Feature Organization (`Application/Features/`):**
Each feature is self-contained with its own namespace to avoid coupling. Features are organized with `Commands/` and `Queries/` subfolders:
- `ApiKeys/` - API key management
- `Audit/` - Audit log queries
- `UserProfile/` - Profile management (avatar, updates)
- `TodoLists/` - Todo list CRUD (Owner-only access)
- `TodoItems/` - Todo item CRUD with toggle (all members)

**Infrastructure (`Application/Infrastructure/`):**
- **Interceptors**: EF Core interceptors for audit trail, soft deletes, and domain events
- **Services**: Concrete implementations (AuthService, BlobStorage, etc.)
- **Persistence**: DbContext, configurations, and migrations

**Auditing System:**
- Automatic audit trail for all BaseEntity changes via `AuditableEntityInterceptor`
- Property-level change tracking with before/after values stored as JSON
- Soft delete conversion via `SoftDeleteInterceptor`
- Query filters automatically exclude soft-deleted records

## Development Commands

### Build & Run
```bash
# Build solution
dotnet build

# Run API (from root)
dotnet run --project src/Api/Api.csproj

# API available at https://localhost:7098/
```

### Testing
```bash
# Unit tests
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj

# Integration tests (requires database)
dotnet test tests/Application.IntegrationTests/Application.IntegrationTests.csproj

# Run single test
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter "TestName"
```

### Database Management
```bash
# Add migration (using Makefile)
make migration name=MigrationName

# Apply pending migrations
make migrate

# Generate SQL script
make migrate-script
```

### Code Quality
```bash
# Format code
dotnet format

# Format style only
dotnet format style

# Run analyzers
dotnet format analyzers
```

## Coding Conventions

- **No XML documentation comments** (`/// <summary>`): Code should be self-documenting
- Use comments only for complex logic, not obvious functionality
- Follow Microsoft C# coding conventions
- EditorConfig settings enforce consistent formatting
- All entities should inherit from `BaseEntity` or `AuditableEntity` for consistency

## Database Configuration

- **Default**: In-memory database for development
- **SQL Server**: Set `"UseInMemoryDatabase": false` in `appsettings.json`
- **Docker SQL Edge**: Available for local development with Docker

## Key Implementation Notes

**Creating New Entities (IMPORTANT):**

Every new entity MUST implement the correct combination of base classes and interfaces. Failure to do so creates security vulnerabilities.

| Interface/Base Class | Purpose | When to Use |
|---------------------|---------|-------------|
| `AuditableEntity` | Base class with Id, audit fields, soft delete | **Always** - all entities must inherit this |
| `IOrganizationScoped` | Adds `OrganizationId` property, enables query filters | **Always for tenant data** - any entity that belongs to an organization |
| `ISoftDeletable` | Soft delete support | Automatic via `AuditableEntity` |

**Example: Correct entity definition**
```csharp
public class MyEntity : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }  // Required by IOrganizationScoped
    // ... other properties
}
```

**CRITICAL: Organization Scoping**
- Entities implementing `IOrganizationScoped` automatically get query filters applied via `ApplicationDbContext`
- Without `IOrganizationScoped`, users can access/modify data from other organizations (security vulnerability)
- When creating child entities, copy `OrganizationId` from the parent:
  ```csharp
  var child = new ChildEntity { OrganizationId = parent.OrganizationId, ... };
  ```

**Entity Inheritance:**
- All entities must inherit from `AuditableEntity` to get automatic auditing and soft delete
- `AuditableEntity` implements `ISoftDeletable` and extends `BaseEntity`
- Audit properties are marked with `[ExcludeFromAudit]` to prevent recursive auditing

**Feature Development:**
- Each feature is self-contained with its own DTOs/models to prevent coupling
- Controllers in `Api/Controllers/` correspond to features in `Application/Features/`
- Use MediatR for CQRS pattern (Commands/Queries/Handlers)

**Soft Delete Usage:**
- Entities implementing `ISoftDeletable` are automatically soft-deleted instead of hard-deleted
- Use `context.EntitySet.IgnoreQueryFilters()` to include soft-deleted records when needed
- All CRUD operations automatically respect soft delete status

**Authentication:**
- JWT-based authentication with Google OAuth support
- Avatar management with blob storage integration
- User profile management with conflict handling for username updates

**Multi-Tenancy:**
- Organization-based scoping via `IOrganizationScoped` interface
- `CurrentUserService` provides current organization context from JWT claims
- Global query filters automatically scope data to current organization
- Role-based access: `Owner` vs `Member` roles per organization

**Todo Management:**
- `TodoList`: Organization-scoped, managed by Owners only
- `TodoItem`: Belongs to a list, has priority (None/Low/Medium/High), optional AssignedToId and DueDate
- Items returned nested within list detail response (no separate GetByList endpoint)
- Toggle endpoint for marking items done/undone
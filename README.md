# .NET 8 Multi-Tenant SaaS API Boilerplate

A production-ready .NET 8 Web API boilerplate using Vertical Slice Architecture, featuring multi-tenancy, JWT + Google OAuth authentication, and automatic auditing.

## Features

- **Multi-Tenancy**: Organization-based data isolation with automatic query filtering
- **Authentication**: JWT tokens with refresh flow + Google OAuth 2.0
- **Vertical Slice Architecture**: Features organized by business domain, not technical layers
- **CQRS Pattern**: Command/Query separation via MediatR
- **Automatic Auditing**: All entity changes tracked with before/after values
- **Soft Deletes**: Entities marked as deleted rather than removed
- **Role-Based Access**: Owner vs Member permissions per organization

## Tech Stack

- [ASP.NET Core 8](https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-8.0)
- [Entity Framework Core 8](https://docs.microsoft.com/en-us/ef/core/) with In-Memory/SQL Server/PostgreSQL support
- [MediatR](https://github.com/jbogard/MediatR) for CQRS
- [FluentValidation](https://fluentvalidation.net/) for request validation
- [AutoMapper](https://automapper.org/) for DTO mapping
- [xUnit](https://xunit.net/), [FluentAssertions](https://fluentassertions.com/), [Moq](https://github.com/moq) for testing
- Azure Blob Storage for file uploads

## Project Structure

```
src/
├── Api/                      # ASP.NET Web API entry point
│   ├── Controllers/          # API endpoints
│   └── appsettings.json      # Configuration
└── Application/              # Business logic (vertical slices)
    ├── Domain/               # Entities, enums, value objects
    │   └── Entities/         # ApplicationUser, Organization, TodoList, TodoItem, etc.
    ├── Features/             # Self-contained feature modules
    │   ├── ApiKeys/          # API key management
    │   ├── Audit/            # Audit log queries
    │   ├── TodoLists/        # Todo list CRUD (Owner-only)
    │   ├── TodoItems/        # Todo item management
    │   └── UserProfile/      # Profile & avatar management
    ├── Infrastructure/       # EF Core, services, interceptors
    └── Common/               # Shared utilities, interfaces, behaviors
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Run the API

```bash
dotnet run --project src/Api/Api.csproj
```

API available at https://localhost:7098 with Swagger UI.

### Commands

```bash
# Build
dotnet build

# Run tests
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj
dotnet test tests/Application.IntegrationTests/Application.IntegrationTests.csproj

# Run single test
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --filter "TestName"

# Database migrations (via Makefile)
make migration name=MigrationName
make migrate

# Code formatting
dotnet format
```

## Database Configuration

**Default**: In-memory database (no setup required)

**SQL Server/PostgreSQL**: Set `"UseInMemoryDatabase": false` in `appsettings.json` and configure the connection string.

### Azure SQL Edge (Docker)

```bash
docker pull mcr.microsoft.com/azure-sql-edge:latest
docker run --cap-add SYS_PTRACE -e 'ACCEPT_EULA=1' -e 'MSSQL_SA_PASSWORD=yourStrong(!)Password' -p 1433:1433 --name azuresqledge -d mcr.microsoft.com/azure-sql-edge
```

## Configuration

Key settings in `appsettings.json`:

- `JwtSettings`: Token signing key, issuer, audience
- `GoogleOAuth`: Client ID/secret for Google authentication
- `AzureStorage`: Blob storage connection for file uploads
- `MailConfiguration`: SMTP settings for email

## Architecture Notes

This project uses **Vertical Slice Architecture** - code is organized by feature rather than technical layer. Each feature (TodoLists, UserProfile, etc.) contains its own Commands, Queries, Handlers, and DTOs.

Benefits:
- Related code is co-located
- Features are decoupled from each other
- Easy to add/modify features without touching unrelated code

For detailed architecture guidance, see `CLAUDE.md`.

## Based On

Originally forked from [Vertical Slice Architecture example](https://nadirbad.dev/posts/vetical-slice-architecture-dotnet/), inspired by:
- [Clean Architecture by Jason Taylor](https://github.com/jasontaylordev/CleanArchitecture)
- [Vertical Slice Architecture by Jimmy Bogard](https://jimmybogard.com/vertical-slice-architecture/)

## License

[MIT License](./LICENSE)

using System.IO;

using Application.Common.Interfaces;
using Application.Domain.Enums;
using Application.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Application.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(basePath, "src", "Api");
        if (!Directory.Exists(configPath))
        {
            configPath = basePath;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString);

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new DateTimeService(),
            new DesignTimeCurrentUserService());
    }

    /// <summary>
    /// Stub implementation of ICurrentUserService for design-time DbContext creation (migrations).
    /// Returns null/default values since there's no HTTP context during migrations.
    /// </summary>
    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string UserEmail => string.Empty;
        public Role Role => Role.User;
        public string DeviceInfo => string.Empty;
        public bool IsApiRequest => false;
        public Guid? OrganizationId => null;
        public OrganizationRole? OrganizationRole => null;
    }
}

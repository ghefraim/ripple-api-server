using System.Reflection;

using Application.Common.Interfaces;
using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models;
using Application.Common.Models.BlobStorage;
using Application.Domain.Entities;
using Application.Infrastructure.Files;
using Application.Infrastructure.Interceptors;
using Application.Infrastructure.Persistence;
using Application.Infrastructure.Services;
using Application.Infrastructure.Services.BlobStorage;

using Ardalis.GuardClauses;

using FluentValidation;

using Infrastructure.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Infrastructure;

public static class InfrastructureServiceConfiguration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        // Configure Identity
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.User.RequireUniqueEmail = true;

            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.AzureStorage));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.Stripe));
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.Billing));

        services.AddMemoryCache();

        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddSingleton<IConcreteStorageClient, ConcreteStorageClient>();
        services.AddScoped<IDomainEventService, DomainEventService>();

        services.AddTransient<ICsvFileBuilder, CsvFileBuilder>();
        services.AddScoped<IMailService, MailService>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<ICryptographyService, CryptographyService>();

        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICookieService, CookieService>();

        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICorrelationIdService, CorrelationIdService>();

        services.AddScoped<IStripeService, StripeService>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();

        services.AddScoped<ICascadeEngine, CascadeEngine>();

        var flightDataProvider = configuration.GetValue<string>("FlightDataProvider") ?? "Local";
        if (flightDataProvider == "Local")
        {
            services.AddScoped<IFlightDataProvider, LocalFlightDataProvider>();
        }

        services.AddHttpClient("GeminiLlm");

        var llmProvider = configuration.GetValue<string>("LlmProvider") ?? "Gemini";
        if (llmProvider == "Gemini")
        {
            services.AddScoped<ILlmProvider, GeminiLlmProvider>();
        }

        return services;
    }
}


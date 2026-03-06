using System.Reflection;

using Api.Filters;

using Application;

using FluentValidation;
using FluentValidation.AspNetCore;

namespace Api;

public static class ApiServiceConfiguration
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        services.AddAuthentication();
        services.AddAuthorization();

        // Extension classes
        // services.AddHttpClient();
        // services.SetupHealthCheck(configuration);
        services.AddHealthChecks();

        services.AddCorsCustom();
        services.AddJWTCustom(configuration);
        services.AddHttpContextAccessor();

        services.AddSingleton<ApiExceptionFilterAttribute>();

        // CORS
        services.AddCors(options => options.AddPolicy(
                "MyAllowedOrigins",
                policy => policy
                    .WithOrigins(
                "http://localhost:8100",
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:60976",
                "https://localhost:8100",
                "http://192.168.1.207:8100",
                "https://localhost",
                "http://localhost",
                "http://localhost:8080",
                "http://localhost:5173",
                "capacitor://localhost",
                "ionic://localhost",
                "https://192.168.1.216:8100")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}


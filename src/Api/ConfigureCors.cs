namespace Api;

public static class ConfigureCors
{
    public static IServiceCollection AddCorsCustom(this IServiceCollection services)
    {
        return services.AddCors(options => options.AddDefaultPolicy(
            policy => policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()));
    }
}

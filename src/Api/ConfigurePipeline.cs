using Api.Hubs;
using Api.Middleware;

using Application.Infrastructure.Persistence;

namespace Api;

public static class ConfigurePipeline
{
    public static async Task<WebApplication> ConfigurePipelineAsync(this WebApplication app)
    {
        using var loggerFactory = LoggerFactory.Create(builder => { });
        using var scope = app.Services.CreateScope();
        if (app.Environment.IsDevelopment())
        {
            await app.InitialiseDatabaseAsync();
        }
        else
        {
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            options.RoutePrefix = string.Empty;
        });

        app.UseCors("MyAllowedOrigins");

        app.UseHttpsRedirection();

        app.UseCorrelationId();

        // app.UseResponseCompression();
        // app.ConfigureHealthCheck();
        if (app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error-development");
        }
        else
        {
            app.UseExceptionHandler("/error");
        }

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<OperationsHub>("/hubs/operations");

        return app;
    }
}

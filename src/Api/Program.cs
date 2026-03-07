using Api;

using Application.Common;
using Application.Infrastructure;
using Application.Infrastructure.Identity;
using Application.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register the Swagger generator, defining 1 or more Swagger documents
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddSwaggerOpenAPI();

builder.Services.AddProblemDetails();

builder.Services.AddCommonServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

// Configure the HTTP request pipeline.
var app = await builder.Build().ConfigurePipelineAsync();

// Initialize database (runs migrations), then roles, then seed demo data
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}
else
{
    // In non-dev, still ensure roles exist (tables must already exist)
    await IdentityInitializer.InitializeAsync(app.Services);
}

app.Run();

public partial class Program { }
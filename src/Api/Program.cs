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

// Initialize roles (must run before seed which assigns roles to demo users)
await IdentityInitializer.InitializeAsync(app.Services);

// Initialize database and seed demo data
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}

app.Run();

public partial class Program { }
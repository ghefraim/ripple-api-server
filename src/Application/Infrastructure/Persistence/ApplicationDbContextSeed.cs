using Application.Domain.Entities;
using Application.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedSampleDataAsync(ApplicationDbContext context)
    {
        await SeedPlansAsync(context);
        await SeedTodoListsAsync(context);
        await SeedAirportDataAsync(context);
    }

    private static async Task SeedPlansAsync(ApplicationDbContext context)
    {
        if (context.Plans.Any())
        {
            return;
        }

        var freePlan = new Plan
        {
            Name = "Free",
            Description = "Get started with basic features",
            MonthlyPrice = 0,
            AnnualPrice = 0,
            IsActive = true,
            SortOrder = 1,
        };

        var proPlan = new Plan
        {
            Name = "Pro",
            Description = "For growing teams that need more power",
            MonthlyPrice = 29,
            AnnualPrice = 290,
            IsActive = true,
            SortOrder = 2,
        };

        var enterprisePlan = new Plan
        {
            Name = "Enterprise",
            Description = "For large organizations with advanced needs",
            MonthlyPrice = 99,
            AnnualPrice = 990,
            IsActive = true,
            SortOrder = 3,
        };

        context.Plans.AddRange(freePlan, proPlan, enterprisePlan);
        await context.SaveChangesAsync();

        // Seed Free Plan Features
        var freePlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = freePlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "3" },
            new() { PlanId = freePlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "5" },
            new() { PlanId = freePlan.Id, FeatureKey = "canExport", FeatureType = "boolean", Value = "false" },
            new() { PlanId = freePlan.Id, FeatureKey = "apiAccess", FeatureType = "boolean", Value = "false" },
        };

        // Seed Pro Plan Features
        var proPlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = proPlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "10" },
            new() { PlanId = proPlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "unlimited" }, // -1 = unlimited
            new() { PlanId = proPlan.Id, FeatureKey = "canExport", FeatureType = "boolean", Value = "true" },
            new() { PlanId = proPlan.Id, FeatureKey = "apiAccess", FeatureType = "boolean", Value = "true" },
        };

        // Seed Enterprise Plan Features
        var enterprisePlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = enterprisePlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "unlimited" }, // -1 = unlimited
            new() { PlanId = enterprisePlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "unlimited" }, // -1 = unlimited
            new() { PlanId = enterprisePlan.Id, FeatureKey = "canExport", FeatureType = "boolean", Value = "true" },
            new() { PlanId = enterprisePlan.Id, FeatureKey = "apiAccess", FeatureType = "boolean", Value = "true" },
        };

        context.PlanFeatures.AddRange(freePlanFeatures);
        context.PlanFeatures.AddRange(proPlanFeatures);
        context.PlanFeatures.AddRange(enterprisePlanFeatures);

        await context.SaveChangesAsync();
    }

    private static async Task SeedTodoListsAsync(ApplicationDbContext context)
    {
        // Seed, if necessary
        if (!context.TodoLists.Any())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAirportDataAsync(ApplicationDbContext context)
    {
        // Seed airport data for each organization that doesn't have an airport config yet
        var organizations = await context.Organizations
            .IgnoreQueryFilters()
            .ToListAsync();

        foreach (var org in organizations)
        {
            var hasAirport = await context.AirportConfigs
                .IgnoreQueryFilters()
                .AnyAsync(a => a.OrganizationId == org.Id);

            if (hasAirport)
                continue;

            await SeedAirportForOrganizationAsync(context, org.Id);
        }
    }

    private static async Task SeedAirportForOrganizationAsync(ApplicationDbContext context, Guid organizationId)
    {
        // Create Cluj airport
        var airport = new AirportConfig
        {
            OrganizationId = organizationId,
            IataCode = "CLJ",
            Name = "Cluj-Napoca International Airport",
            Timezone = "Europe/Bucharest",
            MinTurnaroundMinutes = 35,
        };
        context.AirportConfigs.Add(airport);
        await context.SaveChangesAsync();

        // Seed 6 gates
        var gates = new List<Gate>
        {
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "A1", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "A2", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "A3", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "B1", GateType = GateType.International, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "B2", GateType = GateType.International, SizeCategory = GateSizeCategory.Wide, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Code = "B3", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
        };
        context.Gates.AddRange(gates);

        // Seed 5 ground crews with shift patterns
        var crews = new List<GroundCrew>
        {
            new() { OrganizationId = organizationId, AirportId = airport.Id, Name = "Alpha", ShiftStart = new TimeOnly(6, 0), ShiftEnd = new TimeOnly(14, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Name = "Beta", ShiftStart = new TimeOnly(6, 0), ShiftEnd = new TimeOnly(14, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Name = "Gamma", ShiftStart = new TimeOnly(10, 0), ShiftEnd = new TimeOnly(18, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Name = "Delta", ShiftStart = new TimeOnly(14, 0), ShiftEnd = new TimeOnly(22, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airport.Id, Name = "Epsilon", ShiftStart = new TimeOnly(14, 0), ShiftEnd = new TimeOnly(22, 0), Status = CrewStatus.Available },
        };
        context.GroundCrews.AddRange(crews);

        await context.SaveChangesAsync();
    }
}
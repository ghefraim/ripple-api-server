using Application.Domain.Entities;
using Application.Domain.Enums;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedSampleDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        await SeedPlansAsync(context);
        await SeedTodoListsAsync(context);
        await SeedDemoDataAsync(context, userManager);
    }

    private static async Task SeedDemoDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        var cljOrg = await SeedOrganizationWithUsersAsync(context, userManager,
            orgName: "CLJ Operations",
            orgDescription: "Cluj-Napoca Airport operations workspace",
            users: new[]
            {
                ("admin@clj.demo", OrganizationRole.Owner),
                ("ops@clj.demo", OrganizationRole.Member),
                ("crew@clj.demo", OrganizationRole.Member),
            });

        var otpOrg = await SeedOrganizationWithUsersAsync(context, userManager,
            orgName: "OTP Operations",
            orgDescription: "Bucharest Henri Coanda Airport operations workspace",
            users: new[]
            {
                ("admin@otp.demo", OrganizationRole.Owner),
                ("ops@otp.demo", OrganizationRole.Member),
                ("crew@otp.demo", OrganizationRole.Member),
            });

        if (cljOrg != null)
            await SeedAirportForOrganizationAsync(context, cljOrg.Id, "CLJ", "Cluj-Napoca International Airport", "Europe/Bucharest");

        if (otpOrg != null)
            await SeedAirportForOrganizationAsync(context, otpOrg.Id, "OTP", "Henri Coanda International Airport", "Europe/Bucharest");
    }

    private static async Task<Organization?> SeedOrganizationWithUsersAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        string orgName,
        string orgDescription,
        (string Email, OrganizationRole Role)[] users)
    {
        var existingOrg = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Name == orgName);

        if (existingOrg != null)
            return null;

        var createdUsers = new List<(ApplicationUser User, OrganizationRole Role)>();
        foreach (var (email, role) in users)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                createdUsers.Add((existing, role));
                continue;
            }

            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                CreatedOn = DateTime.UtcNow,
            };

            var result = await userManager.CreateAsync(user, "Demo123!");
            if (!result.Succeeded)
                continue;

            await userManager.AddToRoleAsync(user, "User");
            createdUsers.Add((user, role));
        }

        if (createdUsers.Count == 0)
            return null;

        var org = new Organization
        {
            Name = orgName,
            Description = orgDescription,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        foreach (var (user, role) in createdUsers)
        {
            context.UserOrganizations.Add(new UserOrganization
            {
                UserId = user.Id,
                OrganizationId = org.Id,
                Role = role,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = "seed",
            });
        }

        await context.SaveChangesAsync();
        return org;
    }

    private static async Task SeedPlansAsync(ApplicationDbContext context)
    {
        if (context.Plans.Any())
            return;

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

        var freePlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = freePlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "3" },
            new() { PlanId = freePlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "5" },
            new() { PlanId = freePlan.Id, FeatureKey = "canExport", FeatureType = "boolean", Value = "false" },
            new() { PlanId = freePlan.Id, FeatureKey = "apiAccess", FeatureType = "boolean", Value = "false" },
        };

        var proPlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = proPlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "10" },
            new() { PlanId = proPlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "unlimited" },
            new() { PlanId = proPlan.Id, FeatureKey = "canExport", FeatureType = "boolean", Value = "true" },
            new() { PlanId = proPlan.Id, FeatureKey = "apiAccess", FeatureType = "boolean", Value = "true" },
        };

        var enterprisePlanFeatures = new List<PlanFeature>
        {
            new() { PlanId = enterprisePlan.Id, FeatureKey = "maxMembers", FeatureType = "limit", Value = "unlimited" },
            new() { PlanId = enterprisePlan.Id, FeatureKey = "maxTodoLists", FeatureType = "limit", Value = "unlimited" },
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
        if (!context.TodoLists.Any())
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAirportForOrganizationAsync(
        ApplicationDbContext context,
        Guid organizationId,
        string iataCode,
        string airportName,
        string timezone)
    {
        var hasAirport = await context.AirportConfigs
            .IgnoreQueryFilters()
            .AnyAsync(a => a.OrganizationId == organizationId);

        if (hasAirport)
            return;

        var airport = new AirportConfig
        {
            OrganizationId = organizationId,
            IataCode = iataCode,
            Name = airportName,
            Timezone = timezone,
            MinTurnaroundMinutes = 35,
            FlightDataSource = FlightDataSource.AviationApi,
        };
        context.AirportConfigs.Add(airport);
        await context.SaveChangesAsync();

        if (iataCode == "CLJ")
            await SeedCljAirportDataAsync(context, organizationId, airport.Id);
        else if (iataCode == "OTP")
            await SeedOtpAirportDataAsync(context, organizationId, airport.Id);
    }

    private static async Task SeedCljAirportDataAsync(ApplicationDbContext context, Guid organizationId, Guid airportId)
    {
        var gates = new List<Gate>
        {
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "A1", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "A2", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "A3", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "B1", GateType = GateType.International, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "B2", GateType = GateType.International, SizeCategory = GateSizeCategory.Wide, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "B3", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
        };
        context.Gates.AddRange(gates);

        var crews = new List<GroundCrew>
        {
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Alpha", ShiftStart = new TimeOnly(6, 0), ShiftEnd = new TimeOnly(14, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Beta", ShiftStart = new TimeOnly(6, 0), ShiftEnd = new TimeOnly(14, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Gamma", ShiftStart = new TimeOnly(10, 0), ShiftEnd = new TimeOnly(18, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Delta", ShiftStart = new TimeOnly(14, 0), ShiftEnd = new TimeOnly(22, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Epsilon", ShiftStart = new TimeOnly(14, 0), ShiftEnd = new TimeOnly(22, 0), Status = CrewStatus.Available },
        };
        context.GroundCrews.AddRange(crews);

        await context.SaveChangesAsync();

        await SeedRulesAsync(context, organizationId, airportId);
    }

    private static async Task SeedOtpAirportDataAsync(ApplicationDbContext context, Guid organizationId, Guid airportId)
    {
        var gates = new List<Gate>
        {
            // Terminal 1 - Domestic / Schengen
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T1-A1", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T1-A2", GateType = GateType.Domestic, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T1-A3", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T1-B1", GateType = GateType.Both, SizeCategory = GateSizeCategory.Wide, IsActive = true },
            // Terminal 2 - International / Non-Schengen
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-C1", GateType = GateType.International, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-C2", GateType = GateType.International, SizeCategory = GateSizeCategory.Wide, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-C3", GateType = GateType.International, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-D1", GateType = GateType.International, SizeCategory = GateSizeCategory.Wide, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-D2", GateType = GateType.Both, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
            new() { OrganizationId = organizationId, AirportId = airportId, Code = "T2-D3", GateType = GateType.International, SizeCategory = GateSizeCategory.Narrow, IsActive = true },
        };
        context.Gates.AddRange(gates);

        var crews = new List<GroundCrew>
        {
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Alpha-T1", ShiftStart = new TimeOnly(5, 0), ShiftEnd = new TimeOnly(13, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Bravo-T1", ShiftStart = new TimeOnly(5, 0), ShiftEnd = new TimeOnly(13, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Charlie-T1", ShiftStart = new TimeOnly(13, 0), ShiftEnd = new TimeOnly(21, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Delta-T2", ShiftStart = new TimeOnly(5, 0), ShiftEnd = new TimeOnly(13, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Echo-T2", ShiftStart = new TimeOnly(9, 0), ShiftEnd = new TimeOnly(17, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Foxtrot-T2", ShiftStart = new TimeOnly(13, 0), ShiftEnd = new TimeOnly(21, 0), Status = CrewStatus.Available },
            new() { OrganizationId = organizationId, AirportId = airportId, Name = "Golf-T2", ShiftStart = new TimeOnly(17, 0), ShiftEnd = new TimeOnly(1, 0), Status = CrewStatus.Available },
        };
        context.GroundCrews.AddRange(crews);

        await context.SaveChangesAsync();

        await SeedRulesAsync(context, organizationId, airportId);
    }

    private static async Task SeedRulesAsync(
        ApplicationDbContext context,
        Guid organizationId,
        Guid airportId)
    {
        var hasRules = await context.OperationalRules
            .IgnoreQueryFilters()
            .AnyAsync(r => r.OrganizationId == organizationId);

        if (hasRules)
            return;

        var rules = new List<OperationalRule>
        {
            new()
            {
                OrganizationId = organizationId,
                AirportId = airportId,
                Name = "Critical turnaround breach",
                Description = "Flag critical when turnaround time drops below 35 minutes for international flights and recommend gate reassignment.",
                IsActive = true,
                RuleJson = """
                {
                    "conditions": [
                        { "field": "turnaround_minutes", "operator": "less_than", "value": 35 },
                        { "field": "flight_type", "operator": "equals", "value": "International" }
                    ],
                    "actions": [
                        { "type": "flag_severity", "value": "Critical" },
                        { "type": "recommend", "value": "gate_reassignment" }
                    ]
                }
                """,
            },
            new()
            {
                OrganizationId = organizationId,
                AirportId = airportId,
                Name = "Long delay alert",
                Description = "Flag critical and notify duty manager when a flight delay exceeds 60 minutes.",
                IsActive = true,
                RuleJson = """
                {
                    "conditions": [
                        { "field": "delay_minutes", "operator": "greater_than", "value": 60 }
                    ],
                    "actions": [
                        { "type": "flag_severity", "value": "Critical" },
                        { "type": "auto_notify", "value": "duty_manager" }
                    ]
                }
                """,
            },
            new()
            {
                OrganizationId = organizationId,
                AirportId = airportId,
                Name = "Gate conflict warning",
                Description = "Flag warning when a delayed flight occupies an international gate, potentially blocking other operations.",
                IsActive = true,
                RuleJson = """
                {
                    "conditions": [
                        { "field": "flight_status", "operator": "equals", "value": "Delayed" },
                        { "field": "gate_type", "operator": "equals", "value": "International" }
                    ],
                    "actions": [
                        { "type": "flag_severity", "value": "Warning" }
                    ]
                }
                """,
            },
            new()
            {
                OrganizationId = organizationId,
                AirportId = airportId,
                Name = "Crew overtime risk",
                Description = "Flag warning and recommend crew reallocation when assigned crew has less than 30 minutes until departure.",
                IsActive = true,
                RuleJson = """
                {
                    "conditions": [
                        { "field": "crew_status", "operator": "equals", "value": "Assigned" },
                        { "field": "time_until_departure", "operator": "less_than", "value": 30 }
                    ],
                    "actions": [
                        { "type": "flag_severity", "value": "Warning" },
                        { "type": "recommend", "value": "crew_reallocation" }
                    ]
                }
                """,
            },
            new()
            {
                OrganizationId = organizationId,
                AirportId = airportId,
                Name = "Departure buffer warning",
                Description = "Flag critical when a non-cancelled flight has less than 15 minutes until departure.",
                IsActive = true,
                RuleJson = """
                {
                    "conditions": [
                        { "field": "time_until_departure", "operator": "less_than", "value": 15 },
                        { "field": "flight_status", "operator": "not_equals", "value": "Cancelled" }
                    ],
                    "actions": [
                        { "type": "flag_severity", "value": "Critical" }
                    ]
                }
                """,
            },
        };

        context.OperationalRules.AddRange(rules);
        await context.SaveChangesAsync();
    }
}

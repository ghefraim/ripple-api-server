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

        // Seed flights
        await SeedFlightsForOrganizationAsync(context, organizationId, airport.Id, gates, crews);

        // Seed operational rules
        await SeedRulesForOrganizationAsync(context, organizationId, airport.Id);
    }

    private static async Task SeedFlightsForOrganizationAsync(
        ApplicationDbContext context,
        Guid organizationId,
        Guid airportId,
        List<Gate> gates,
        List<GroundCrew> crews)
    {
        var hasFlights = await context.Flights
            .IgnoreQueryFilters()
            .AnyAsync(f => f.OrganizationId == organizationId);

        if (hasFlights)
            return;

        var today = DateTime.UtcNow.Date;
        var gateMap = gates.ToDictionary(g => g.Code, g => g.Id);
        var crewMap = crews.ToDictionary(c => c.Name, c => c.Id);

        // Turnaround Pair 1 (demo pair — 45 min window): W6-2901 / W6-2902 at gate A3, crew Alpha
        var w6_2901 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "W6-2901", Airline = "Wizz Air",
            Origin = "London Luton", Destination = "Cluj-Napoca",
            Direction = FlightDirection.Arrival, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(10).AddMinutes(30),
            EstimatedTime = today.AddHours(10).AddMinutes(30),
            GateId = gateMap["A3"], CrewId = crewMap["Alpha"],
        };
        var w6_2902 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "W6-2902", Airline = "Wizz Air",
            Origin = "Cluj-Napoca", Destination = "London Luton",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(11).AddMinutes(15),
            EstimatedTime = today.AddHours(11).AddMinutes(15),
            GateId = gateMap["A3"], CrewId = crewMap["Alpha"],
        };

        // Turnaround Pair 2 (tight — 40 min): RO-361 / RO-362 at gate A1
        var ro_361 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "RO-361", Airline = "TAROM",
            Origin = "Bucharest Henri Coanda", Destination = "Cluj-Napoca",
            Direction = FlightDirection.Arrival, FlightType = FlightType.Domestic,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(8),
            EstimatedTime = today.AddHours(8),
            GateId = gateMap["A1"], CrewId = crewMap["Beta"],
        };
        var ro_362 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "RO-362", Airline = "TAROM",
            Origin = "Cluj-Napoca", Destination = "Bucharest Henri Coanda",
            Direction = FlightDirection.Departure, FlightType = FlightType.Domestic,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(8).AddMinutes(40),
            EstimatedTime = today.AddHours(8).AddMinutes(40),
            GateId = gateMap["A1"], CrewId = crewMap["Beta"],
        };

        // Turnaround Pair 3 (tight — 35 min): FR-1823 / FR-1824 at gate B1
        var fr_1823 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "FR-1823", Airline = "Ryanair",
            Origin = "Milan Bergamo", Destination = "Cluj-Napoca",
            Direction = FlightDirection.Arrival, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(12),
            EstimatedTime = today.AddHours(12),
            GateId = gateMap["B1"], CrewId = crewMap["Gamma"],
        };
        var fr_1824 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "FR-1824", Airline = "Ryanair",
            Origin = "Cluj-Napoca", Destination = "Milan Bergamo",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(12).AddMinutes(35),
            EstimatedTime = today.AddHours(12).AddMinutes(35),
            GateId = gateMap["B1"], CrewId = crewMap["Gamma"],
        };

        // Turnaround Pair 4 (comfortable — 60 min): LH-1417 / LH-1418 at gate B2
        var lh_1417 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "LH-1417", Airline = "Lufthansa",
            Origin = "Munich", Destination = "Cluj-Napoca",
            Direction = FlightDirection.Arrival, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(14),
            EstimatedTime = today.AddHours(14),
            GateId = gateMap["B2"], CrewId = crewMap["Delta"],
        };
        var lh_1418 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "LH-1418", Airline = "Lufthansa",
            Origin = "Cluj-Napoca", Destination = "Munich",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(15),
            EstimatedTime = today.AddHours(15),
            GateId = gateMap["B2"], CrewId = crewMap["Delta"],
        };

        // Standalone flights (no turnaround pair)
        var flights = new List<Flight>
        {
            w6_2901, w6_2902, ro_361, ro_362, fr_1823, fr_1824, lh_1417, lh_1418,
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "W6-2907", Airline = "Wizz Air",
                Origin = "Paris Beauvais", Destination = "Cluj-Napoca",
                Direction = FlightDirection.Arrival, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(6).AddMinutes(30),
                EstimatedTime = today.AddHours(6).AddMinutes(30),
                GateId = gateMap["A2"], CrewId = crewMap["Beta"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "W6-3105", Airline = "Wizz Air",
                Origin = "Cluj-Napoca", Destination = "Dortmund",
                Direction = FlightDirection.Departure, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(7),
                EstimatedTime = today.AddHours(7),
                GateId = gateMap["A2"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "RO-371", Airline = "TAROM",
                Origin = "Iasi", Destination = "Cluj-Napoca",
                Direction = FlightDirection.Arrival, FlightType = FlightType.Domestic,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(9).AddMinutes(30),
                EstimatedTime = today.AddHours(9).AddMinutes(30),
                GateId = gateMap["A1"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "RO-391", Airline = "TAROM",
                Origin = "Cluj-Napoca", Destination = "Iasi",
                Direction = FlightDirection.Departure, FlightType = FlightType.Domestic,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(11),
                EstimatedTime = today.AddHours(11),
                GateId = gateMap["A1"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "W6-2905", Airline = "Wizz Air",
                Origin = "Cluj-Napoca", Destination = "Barcelona",
                Direction = FlightDirection.Departure, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(13),
                EstimatedTime = today.AddHours(13),
                GateId = gateMap["A3"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "FR-1831", Airline = "Ryanair",
                Origin = "Brussels Charleroi", Destination = "Cluj-Napoca",
                Direction = FlightDirection.Arrival, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(15).AddMinutes(30),
                EstimatedTime = today.AddHours(15).AddMinutes(30),
                GateId = gateMap["B3"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "RO-381", Airline = "TAROM",
                Origin = "Cluj-Napoca", Destination = "Timisoara",
                Direction = FlightDirection.Departure, FlightType = FlightType.Domestic,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(16),
                EstimatedTime = today.AddHours(16),
                GateId = gateMap["A2"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "W6-2911", Airline = "Wizz Air",
                Origin = "Rome Fiumicino", Destination = "Cluj-Napoca",
                Direction = FlightDirection.Arrival, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(17),
                EstimatedTime = today.AddHours(17),
                GateId = gateMap["B1"], CrewId = crewMap["Delta"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "LH-1419", Airline = "Lufthansa",
                Origin = "Cluj-Napoca", Destination = "Frankfurt",
                Direction = FlightDirection.Departure, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(18),
                EstimatedTime = today.AddHours(18),
                GateId = gateMap["B2"], CrewId = crewMap["Epsilon"],
            },
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "FR-1841", Airline = "Ryanair",
                Origin = "Cluj-Napoca", Destination = "Madrid",
                Direction = FlightDirection.Departure, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(19),
                EstimatedTime = today.AddHours(19),
                GateId = gateMap["B3"], CrewId = crewMap["Epsilon"],
            },
        };

        context.Flights.AddRange(flights);
        await context.SaveChangesAsync();

        // Set turnaround pair references (each flight points to its pair)
        w6_2901.TurnaroundPairId = w6_2902.Id;
        w6_2902.TurnaroundPairId = w6_2901.Id;

        ro_361.TurnaroundPairId = ro_362.Id;
        ro_362.TurnaroundPairId = ro_361.Id;

        fr_1823.TurnaroundPairId = fr_1824.Id;
        fr_1824.TurnaroundPairId = fr_1823.Id;

        lh_1417.TurnaroundPairId = lh_1418.Id;
        lh_1418.TurnaroundPairId = lh_1417.Id;

        // Update crew status to Assigned for crews with flights
        var alphaCrewEntity = crews.First(c => c.Name == "Alpha");
        alphaCrewEntity.Status = CrewStatus.Assigned;

        await context.SaveChangesAsync();
    }

    private static async Task SeedRulesForOrganizationAsync(
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
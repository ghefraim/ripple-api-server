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
        var org = await SeedDemoUsersAsync(context, userManager);
        if (org != null)
        {
            await SeedAirportForOrganizationAsync(context, org.Id);
        }
        else
        {
            await SeedAirportDataAsync(context);
        }
    }

    private static async Task<Organization?> SeedDemoUsersAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        // Check if demo org already exists
        var existingOrg = await context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Name == "Ripple Demo");

        if (existingOrg != null)
            return null;

        // Create demo users
        var demoUsers = new[]
        {
            new { Email = "admin@ripple.demo", UserName = "admin@ripple.demo", Password = "Demo123!", Role = OrganizationRole.Owner },
            new { Email = "ops@ripple.demo", UserName = "ops@ripple.demo", Password = "Demo123!", Role = OrganizationRole.Member },
            new { Email = "crew@ripple.demo", UserName = "crew@ripple.demo", Password = "Demo123!", Role = OrganizationRole.Member },
        };

        var createdUsers = new List<(ApplicationUser User, OrganizationRole Role)>();
        foreach (var demo in demoUsers)
        {
            var existing = await userManager.FindByEmailAsync(demo.Email);
            if (existing != null)
            {
                createdUsers.Add((existing, demo.Role));
                continue;
            }

            var user = new ApplicationUser
            {
                Email = demo.Email,
                UserName = demo.UserName,
                EmailConfirmed = true,
                CreatedOn = DateTime.UtcNow,
            };

            var result = await userManager.CreateAsync(user, demo.Password);
            if (!result.Succeeded)
                continue;

            await userManager.AddToRoleAsync(user, "User");
            createdUsers.Add((user, demo.Role));
        }

        if (createdUsers.Count == 0)
            return null;

        // Create demo organization
        var org = new Organization
        {
            Name = "Ripple Demo",
            Description = "Demo airport operations workspace",
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        // Link users to org
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

    private static async Task SeedAirportDataAsync(ApplicationDbContext context)
    {
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
        var hasAirport = await context.AirportConfigs
            .IgnoreQueryFilters()
            .AnyAsync(a => a.OrganizationId == organizationId);

        if (hasAirport)
            return;

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

        await SeedFlightsAsync(context, organizationId, airport.Id, gates, crews);
        await SeedDisruptionsAsync(context, organizationId, airport.Id);
        await SeedRulesAsync(context, organizationId, airport.Id);
    }

    private static async Task SeedFlightsAsync(
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

        var w6_2901 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "W6-2901", Airline = "Wizz Air",
            Origin = "LTN", Destination = "CLJ",
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
            Origin = "CLJ", Destination = "LTN",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(11).AddMinutes(15),
            EstimatedTime = today.AddHours(11).AddMinutes(15),
            GateId = gateMap["A3"], CrewId = crewMap["Alpha"],
        };

        var ro_361 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "RO-361", Airline = "TAROM",
            Origin = "OTP", Destination = "CLJ",
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
            Origin = "CLJ", Destination = "OTP",
            Direction = FlightDirection.Departure, FlightType = FlightType.Domestic,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(8).AddMinutes(40),
            EstimatedTime = today.AddHours(8).AddMinutes(40),
            GateId = gateMap["A1"], CrewId = crewMap["Beta"],
        };

        var fr_1823 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "FR-1823", Airline = "Ryanair",
            Origin = "BGY", Destination = "CLJ",
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
            Origin = "CLJ", Destination = "BGY",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(12).AddMinutes(35),
            EstimatedTime = today.AddHours(12).AddMinutes(35),
            GateId = gateMap["B1"], CrewId = crewMap["Gamma"],
        };

        var lh_1417 = new Flight
        {
            OrganizationId = organizationId, AirportId = airportId,
            FlightNumber = "LH-1417", Airline = "Lufthansa",
            Origin = "MUC", Destination = "CLJ",
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
            Origin = "CLJ", Destination = "MUC",
            Direction = FlightDirection.Departure, FlightType = FlightType.International,
            Status = FlightStatus.OnTime,
            ScheduledTime = today.AddHours(15),
            EstimatedTime = today.AddHours(15),
            GateId = gateMap["B2"], CrewId = crewMap["Delta"],
        };

        var flights = new List<Flight>
        {
            w6_2901, w6_2902, ro_361, ro_362, fr_1823, fr_1824, lh_1417, lh_1418,
            new()
            {
                OrganizationId = organizationId, AirportId = airportId,
                FlightNumber = "W6-2907", Airline = "Wizz Air",
                Origin = "BVA", Destination = "CLJ",
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
                Origin = "CLJ", Destination = "DTM",
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
                Origin = "IAS", Destination = "CLJ",
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
                Origin = "CLJ", Destination = "IAS",
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
                Origin = "CLJ", Destination = "BCN",
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
                Origin = "CRL", Destination = "CLJ",
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
                Origin = "CLJ", Destination = "TSR",
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
                Origin = "FCO", Destination = "CLJ",
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
                Origin = "CLJ", Destination = "FRA",
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
                Origin = "CLJ", Destination = "MAD",
                Direction = FlightDirection.Departure, FlightType = FlightType.International,
                Status = FlightStatus.OnTime,
                ScheduledTime = today.AddHours(19),
                EstimatedTime = today.AddHours(19),
                GateId = gateMap["B3"], CrewId = crewMap["Epsilon"],
            },
        };

        context.Flights.AddRange(flights);
        await context.SaveChangesAsync();

        w6_2901.TurnaroundPairId = w6_2902.Id;
        w6_2902.TurnaroundPairId = w6_2901.Id;
        ro_361.TurnaroundPairId = ro_362.Id;
        ro_362.TurnaroundPairId = ro_361.Id;
        fr_1823.TurnaroundPairId = fr_1824.Id;
        fr_1824.TurnaroundPairId = fr_1823.Id;
        lh_1417.TurnaroundPairId = lh_1418.Id;
        lh_1418.TurnaroundPairId = lh_1417.Id;

        var alphaCrewEntity = crews.First(c => c.Name == "Alpha");
        alphaCrewEntity.Status = CrewStatus.Assigned;

        await context.SaveChangesAsync();
    }

    private static async Task SeedDisruptionsAsync(
        ApplicationDbContext context,
        Guid organizationId,
        Guid airportId)
    {
        var hasDisruptions = await context.Disruptions
            .IgnoreQueryFilters()
            .AnyAsync(d => d.OrganizationId == organizationId);

        if (hasDisruptions)
            return;

        var flights = await context.Flights
            .IgnoreQueryFilters()
            .Where(f => f.OrganizationId == organizationId)
            .ToListAsync();

        var flightMap = flights.ToDictionary(f => f.FlightNumber, f => f);
        var now = DateTime.UtcNow;

        // Disruption 1: FR-1823 gate change due to maintenance on B1
        var gateChange = new Disruption
        {
            OrganizationId = organizationId,
            AirportId = airportId,
            FlightId = flightMap["FR-1823"].Id,
            Type = DisruptionType.GateChange,
            DetailsJson = """{"original_gate":"B1","new_gate":"B3","reason":"Unscheduled jet bridge maintenance on B1"}""",
            ReportedBy = "Maintenance Ops",
            ReportedAt = now.AddMinutes(-15),
            Status = DisruptionStatus.Active,
        };
        context.Disruptions.Add(gateChange);
        await context.SaveChangesAsync();

        context.CascadeImpacts.Add(new CascadeImpact
        {
            OrganizationId = organizationId,
            DisruptionId = gateChange.Id,
            AffectedFlightId = flightMap["FR-1824"].Id,
            ImpactType = CascadeImpactType.GateConflict,
            Severity = Severity.Warning,
            Details = """{"conflict_gate":"B3","conflicting_flight":"FR-1831","overlap_window":"12:00-12:35 vs 15:30","resolution":"Tight but manageable if FR-1823/1824 departs on time"}""",
        });

        context.ActionPlans.Add(new ActionPlan
        {
            OrganizationId = organizationId,
            DisruptionId = gateChange.Id,
            LlmOutputText = "Gate B1 is offline for jet bridge maintenance. FR-1823 rerouted to B3. The turnaround pair FR-1823/1824 must depart by 12:35 to avoid conflict with FR-1831 at 15:30. Crew Gamma is already assigned — no crew change needed.",
            ActionsJson = """[{"action":"reassign_gate","flight":"FR-1823","from_gate":"B1","to_gate":"B3"},{"action":"reassign_gate","flight":"FR-1824","from_gate":"B1","to_gate":"B3"},{"action":"monitor","flight":"FR-1824","condition":"must_depart_by_12:35"}]""",
            GeneratedAt = now.AddMinutes(-13),
        });
        await context.SaveChangesAsync();

        // Disruption 2: RO-361 — 20 min delay (resolved)
        var ro_361 = flightMap["RO-361"];
        ro_361.Status = FlightStatus.Delayed;
        ro_361.EstimatedTime = ro_361.ScheduledTime.AddMinutes(20);

        var delay = new Disruption
        {
            OrganizationId = organizationId,
            AirportId = airportId,
            FlightId = ro_361.Id,
            Type = DisruptionType.Delay,
            DetailsJson = """{"delayMinutes":20,"reason":"Late inbound aircraft from Bucharest","originalEta":"08:00","newEta":"08:20"}""",
            ReportedBy = "Airline Ops (TAROM)",
            ReportedAt = now.AddMinutes(-60),
            Status = DisruptionStatus.Resolved,
        };
        context.Disruptions.Add(delay);
        await context.SaveChangesAsync();

        var ro_362 = flightMap["RO-362"];
        ro_362.Status = FlightStatus.Delayed;
        ro_362.EstimatedTime = ro_362.ScheduledTime.AddMinutes(15);

        context.CascadeImpacts.Add(new CascadeImpact
        {
            OrganizationId = organizationId,
            DisruptionId = delay.Id,
            AffectedFlightId = ro_362.Id,
            ImpactType = CascadeImpactType.TurnaroundBreach,
            Severity = Severity.Warning,
            Details = """{"turnaround_available_minutes":20,"minimum_required_minutes":35,"status":"resolved_with_delay"}""",
        });

        context.CascadeImpacts.Add(new CascadeImpact
        {
            OrganizationId = organizationId,
            DisruptionId = delay.Id,
            AffectedFlightId = flightMap["RO-371"].Id,
            ImpactType = CascadeImpactType.DownstreamDelay,
            Severity = Severity.Info,
            Details = """{"reason":"Gate A1 occupied 15 min longer than scheduled","impact_minutes":15,"status":"absorbed_by_buffer"}""",
        });

        context.ActionPlans.Add(new ActionPlan
        {
            OrganizationId = organizationId,
            DisruptionId = delay.Id,
            LlmOutputText = "RO-361 delayed 20 minutes due to late inbound from Bucharest. Turnaround with RO-362 is now below minimum. Recommended delaying RO-362 by 15 minutes and expediting ground handling. RO-371 at gate A1 has sufficient buffer to absorb the knock-on delay.",
            ActionsJson = """[{"action":"delay_departure","flight":"RO-362","new_time":"08:55"},{"action":"expedite_handling","flight":"RO-361"},{"action":"monitor","flight":"RO-371","condition":"gate_availability"}]""",
            GeneratedAt = now.AddMinutes(-58),
        });

        await context.SaveChangesAsync();
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

using Application.Domain.Entities;

namespace Application.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedSampleDataAsync(ApplicationDbContext context)
    {
        await SeedPlansAsync(context);
        await SeedTodoListsAsync(context);
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
            // context.TodoLists.Add(new TodoList
            // {
            //     Title = "Shopping",
            //     Colour = Colour.Blue,
            //     Items =
            //         {
            //             new TodoItem { Title = "Apples", Done = true },
            //             new TodoItem { Title = "Milk", Done = true },
            //             new TodoItem { Title = "Bread", Done = true },
            //             new TodoItem { Title = "Toilet paper" },
            //             new TodoItem { Title = "Pasta" },
            //             new TodoItem { Title = "Tissues" },
            //             new TodoItem { Title = "Tuna" },
            //             new TodoItem { Title = "Water" },
            //         },
            // });

            await context.SaveChangesAsync();
        }
    }
}
using Application.Common.Extensions;
using Application.Domain.Entities;
using Application.Domain.Enums;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Infrastructure.Identity;

public static class IdentityInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        // Create roles for each enum value
        foreach (Role role in Enum.GetValues(typeof(Role)))
        {
            var roleName = role.ToIdentityRole();
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }

    public static async Task<bool> AssignDefaultRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, Role.User))
        {
            return await userManager.AddToRoleAsync(user, Role.User);
        }

        return true;
    }
}
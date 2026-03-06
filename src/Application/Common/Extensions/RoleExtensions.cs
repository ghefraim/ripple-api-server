using Application.Domain.Entities;
using Application.Domain.Enums;

using Microsoft.AspNetCore.Identity;

namespace Application.Common.Extensions;

public static class RoleExtensions
{
    public static string ToIdentityRole(this Role role) => role.ToString();

    public static Role ToRole(this string roleString)
    {
        return Enum.TryParse<Role>(roleString, out var role) ? role : Role.User;
    }

    public static async Task<Role> GetHighestRole(this UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Select(r => r.ToRole())
                   .OrderByDescending(r => r)
                   .FirstOrDefault();
    }

    public static async Task<bool> AddToRoleAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user, Role role)
    {
        var result = await userManager.AddToRoleAsync(user, role.ToIdentityRole());
        return result.Succeeded;
    }

    public static async Task<bool> IsInRoleAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user, Role role)
    {
        return await userManager.IsInRoleAsync(user, role.ToIdentityRole());
    }
}
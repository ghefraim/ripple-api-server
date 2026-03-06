using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;

using FluentValidation;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.UserProfile.UpdateProfile;

[Authorize]
public record UpdateProfileCommand(string UserName) : IRequest<UpdateProfileResponse>;

public class UpdateProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.User;
}

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(v => v.UserName)
            .NotEmpty()
            .WithMessage("Username is required.")
            .Length(3, 50)
            .WithMessage("Username must be between 3 and 50 characters.")
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username can only contain letters, numbers, periods, hyphens, and underscores.");
    }
}

internal sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UpdateProfileResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateProfileCommandHandler(
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager)
    {
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public async Task<UpdateProfileResponse> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User not authenticated"));
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Check if username is already taken by another user
        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.UserName == request.UserName && u.Id != user.Id, cancellationToken);

        if (existingUser != null)
        {
            throw new ConflictException("Username is already taken");
        }

        user.UserName = request.UserName;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to update profile: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return new UpdateProfileResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Role = await _userManager.GetHighestRole(user),
        };
    }
}

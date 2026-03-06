using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.BlobStorage;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;

using Microsoft.AspNetCore.Identity;

namespace Application.Features.UserProfile.DeleteAvatar;

[Authorize]
public record DeleteAvatarCommand : IRequest<DeleteAvatarResponse>;

public class DeleteAvatarResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.User;
    public string? AvatarUrl { get; set; }
}

internal sealed class DeleteAvatarCommandHandler : IRequestHandler<DeleteAvatarCommand, DeleteAvatarResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConcreteStorageClient _storageClient;

    public DeleteAvatarCommandHandler(
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IConcreteStorageClient storageClient)
    {
        _currentUserService = currentUserService;
        _userManager = userManager;
        _storageClient = storageClient;
    }

    public async Task<DeleteAvatarResponse> Handle(DeleteAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User not authenticated"));
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Check if user has an avatar to delete
        if (string.IsNullOrEmpty(user.AvatarBlobName))
        {
            throw new InvalidOperationException("User has no avatar to delete");
        }

        // Delete avatar from blob storage
        try
        {
            await _storageClient.DeleteFileAsync(user.AvatarBlobName, cancellationToken);
        }
        catch (Exception)
        {
            // Continue even if blob deletion fails - we still want to clear the user's avatar reference
            // The blob might have been already deleted or the storage might be temporarily unavailable
        }

        // Clear avatar information from user
        user.AvatarBlobName = null;
        user.AvatarUrl = null;
        user.AvatarUploadedAt = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to update user avatar: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return new DeleteAvatarResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Role = await _userManager.GetHighestRole(user),
            AvatarUrl = user.AvatarUrl, // Will be null after deletion
        };
    }
}

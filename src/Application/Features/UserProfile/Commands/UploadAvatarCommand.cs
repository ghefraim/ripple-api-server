using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models.BlobStorage;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Application.Features.UserProfile.UploadAvatar;

[Authorize]
public record UploadAvatarCommand(IFormFile File) : IRequest<UploadAvatarResponse>;

public class UploadAvatarResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.User;
    public string? AvatarUrl { get; set; }
}

public class UploadAvatarCommandValidator : AbstractValidator<UploadAvatarCommand>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/jpg", "image/png", "image/webp" };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public UploadAvatarCommandValidator()
    {
        RuleFor(v => v.File)
            .NotNull()
            .WithMessage("File is required.");

        RuleFor(v => v.File.Length)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)}MB.")
            .GreaterThan(0)
            .WithMessage("File cannot be empty.");

        RuleFor(v => v.File.ContentType)
            .Must(contentType => AllowedContentTypes.Contains(contentType?.ToLowerInvariant()))
            .WithMessage($"File must be one of the following types: {string.Join(", ", AllowedContentTypes)}");

        RuleFor(v => v.File.FileName)
            .Must(fileName =>
            {
                if (string.IsNullOrEmpty(fileName)) return false;
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return AllowedExtensions.Contains(extension);
            })
            .WithMessage($"File must have one of the following extensions: {string.Join(", ", AllowedExtensions)}");
    }
}

internal sealed class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, UploadAvatarResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConcreteStorageClient _storageClient;

    public UploadAvatarCommandHandler(
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IConcreteStorageClient storageClient)
    {
        _currentUserService = currentUserService;
        _userManager = userManager;
        _storageClient = storageClient;
    }

    public async Task<UploadAvatarResponse> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(_currentUserService.UserId ?? throw new UnauthorizedAccessException("User not authenticated"));
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Delete existing avatar if present
        if (!string.IsNullOrEmpty(user.AvatarBlobName))
        {
            try
            {
                await _storageClient.DeleteFileAsync(user.AvatarBlobName, cancellationToken);
            }
            catch
            {
                // Continue if deletion fails - we'll overwrite with new avatar
            }
        }

        // Generate unique blob name
        var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var blobName = $"avatars/{user.Id}-{timestamp}{fileExtension}";

        // Upload new avatar
        var inputBlob = new InputBlob
        {
            BlobName = blobName,
            ContentType = request.File.ContentType,
            UploadType = UploadType.Stream,
            StreamFile = request.File.OpenReadStream(),
            Metadata = new Dictionary<string, string>
            {
                { "userId", user.Id.ToString() },
                { "uploadedAt", DateTime.UtcNow.ToString("O") },
                { "originalFileName", request.File.FileName }
            }
        };

        var uploadResult = await _storageClient.UploadAsync(inputBlob, cancellationToken);

        // Update user with new avatar information
        user.AvatarBlobName = uploadResult.BlobName;
        user.AvatarUrl = null; // We'll generate SAS URLs on demand
        user.AvatarUploadedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            // Clean up uploaded blob if user update fails
            try
            {
                await _storageClient.DeleteFileAsync(uploadResult.BlobName, cancellationToken);
            }
            catch
            {
                // Log but don't throw - user update failure is more important
            }

            throw new InvalidOperationException($"Failed to update user avatar: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Generate SAS URL for the response
        string? avatarUrl = null;
        try
        {
            var sasUri = _storageClient.GetFileSasUri(uploadResult.BlobName, TimeSpan.FromHours(24));
            avatarUrl = sasUri.AbsoluteUri;
        }
        catch
        {
            // If SAS URL generation fails, leave avatarUrl as null
        }

        return new UploadAvatarResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Role = await _userManager.GetHighestRole(user),
            AvatarUrl = avatarUrl,
        };
    }
}

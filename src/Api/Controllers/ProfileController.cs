using Application.Features.UserProfile.DeleteAvatar;
using Application.Features.UserProfile.UpdateProfile;
using Application.Features.UserProfile.UploadAvatar;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class ProfileController : ApiControllerBase
{
    /// <summary>
    /// Update user profile
    /// </summary>
    [HttpPut("[action]")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileCommand request)
    {
        return Ok(await Mediator.Send(request));
    }

    /// <summary>
    /// Upload user avatar
    /// </summary>
    [HttpPost("[action]")]
    public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarCommand request)
    {
        return Ok(await Mediator.Send(request));
    }

    /// <summary>
    /// Delete user avatar
    /// </summary>
    [HttpDelete("[action]")]
    public async Task<IActionResult> DeleteAvatar()
    {
        return Ok(await Mediator.Send(new DeleteAvatarCommand()));
    }
}
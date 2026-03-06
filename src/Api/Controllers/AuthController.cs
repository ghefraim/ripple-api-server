using Application.Common.Interfaces;
using Application.Common.Models.Organization;
using Application.Common.Models.User;

using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IOrganizationService _organizationService;

    public AuthController(IAuthService authService, IOrganizationService organizationService)
    {
        _authService = authService;
        _organizationService = organizationService;
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> LogIn(EmailLogInRequest request)
        => Ok(await _authService.LogInAsync(request));

    [HttpPost("[action]")]
    public async Task<IActionResult> Register(EmailSignUpRequest request)
        => Ok(await _authService.RegisterAsync(request));

    [HttpDelete("[action]")]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return Ok();
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> RefreshToken()
    {
        var newAccessToken = await _authService.RefreshTokenAsync();
        return Ok(new { accessToken = newAccessToken });
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> Current()
        => Ok(await _authService.GetCurrentUserAsync());

    [HttpPost("[action]")]
    public async Task<IActionResult> SelectOrganization(SelectOrganizationRequest request)
        => Ok(await _organizationService.SelectOrganizationAsync(request));

    [HttpPost("[action]")]
    public async Task<IActionResult> CreateOrganization(CreateOrganizationRequest request)
        => Ok(await _organizationService.CreateOrganizationAsync(request));

    [HttpPost("[action]")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleCredentialRequest request)
        => Ok(await _authService.GoogleLoginAsync(request.Credential));

    [HttpPost("[action]")]
    public async Task<IActionResult> GoogleCallback([FromBody] GoogleCallbackRequest request)
        => Ok(await _authService.GoogleCallbackAsync(request.Code));

    [HttpPost("[action]")]
    public async Task<IActionResult> SendResetPasswordEmail(ResetPasswordRequest request)
    {
        await _authService.SendResetPasswordEmailAsync(request);
        return Ok();
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> ResetPassword(ConfirmResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok();
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
        => Ok(await _authService.ConfirmEmailAsync(request));
}

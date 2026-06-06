using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SurveyMaker.Infrastructure.Identity;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        var redirectUrl = Url.Action(nameof(GoogleCallback), "Account", new { returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUrl);

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return Redirect("/");

        // Try signing in with an existing linked account
        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (result.Succeeded)
        {
            await UpdateLastLogin(info.ProviderKey, info.LoginProvider);
            return Redirect(returnUrl);
        }

        // First-time login — create account from Google profile
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return Redirect("/");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            // Log errors without exposing internal details to the client
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Redirect("/");
        }

        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: false);

        return Redirect(returnUrl);
    }

    [HttpGet("user")]
    public IActionResult GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new { isAuthenticated = false });

        return Ok(new
        {
            isAuthenticated = true,
            name = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email),
            email = User.FindFirstValue(ClaimTypes.Email)
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok();
    }

    private async Task UpdateLastLogin(string providerKey, string loginProvider)
    {
        var user = await userManager.FindByLoginAsync(loginProvider, providerKey);
        if (user is not null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
        }
    }
}

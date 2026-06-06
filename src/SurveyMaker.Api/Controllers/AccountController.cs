using System.Security.Claims;
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
    UserManager<ApplicationUser> userManager,
    ILogger<AccountController> logger) : ControllerBase
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
        {
            logger.LogWarning("Google callback received but no external login info found (correlation failure or direct navigation).");
            return Redirect("/");
        }

        // ── Path 1: existing account with Google already linked ──────────────
        var signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            logger.LogInformation("User {Key} signed in via existing Google login.", info.ProviderKey);
            await UpdateLastLogin(info.LoginProvider, info.ProviderKey);
            return Redirect(returnUrl);
        }

        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("User {Key} attempted login but account is locked out.", info.ProviderKey);
            return Redirect("/");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Google login for key {Key} did not include an email claim.", info.ProviderKey);
            return Redirect("/");
        }

        // ── Path 2: account exists but Google login not yet linked ────────────
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            logger.LogInformation("Linking Google login to existing account for {Email}.", email);

            var addLoginResult = await userManager.AddLoginAsync(existingUser, info);
            if (!addLoginResult.Succeeded)
            {
                logger.LogError("Failed to link Google login for {Email}: {Errors}",
                    email, string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                return Redirect("/");
            }

            existingUser.LastLoginAt = DateTime.UtcNow;
            await userManager.UpdateAsync(existingUser);
            await signInManager.SignInAsync(existingUser, isPersistent: false);
            return Redirect(returnUrl);
        }

        // ── Path 3: brand-new user ────────────────────────────────────────────
        logger.LogInformation("Creating new account for {Email}.", email);

        var newUser = new ApplicationUser
        {
            UserName    = email,
            Email       = email,
            EmailConfirmed = true,
            DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
            CreatedAt   = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create account for {Email}: {Errors}",
                email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return Redirect("/");
        }

        await userManager.AddLoginAsync(newUser, info);
        await signInManager.SignInAsync(newUser, isPersistent: false);

        logger.LogInformation("New account created and signed in for {Email}.", email);
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
            name  = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email),
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

    private async Task UpdateLastLogin(string loginProvider, string providerKey)
    {
        var user = await userManager.FindByLoginAsync(loginProvider, providerKey);
        if (user is not null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
        }
    }
}

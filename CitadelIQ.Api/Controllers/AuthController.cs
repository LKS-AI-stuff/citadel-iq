using CitadelIQ.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AppAuthenticationOptions = CitadelIQ.Application.Options.AuthenticationOptions;

namespace CitadelIQ.Api.Controllers;

/// <summary>Browser navigations (not XHR): start sign-in, sign out.</summary>
[AllowAnonymous]
[Route("auth")]
public class AuthController(IOptions<AppAuthenticationOptions> options) : ControllerBase
{
    private bool UsesOidc => options.Value.Mode == AppAuthenticationOptions.OidcMode;

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        // Only local paths: never an open redirect.
        var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

        if (!UsesOidc)
        {
            return Redirect($"/auth/dev-login?returnUrl={Uri.EscapeDataString(target)}");
        }

        return Challenge(new AuthenticationProperties { RedirectUri = target }, AuthenticationSetup.OidcScheme);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (UsesOidc && User.Identity?.IsAuthenticated == true)
        {
            // Clears the cookie, then sends the browser to the identity provider's end-session endpoint (and back to
            // "/"), so the next sign-in prompts again.
            return SignOut(new AuthenticationProperties { RedirectUri = "/" }, AuthenticationSetup.CookieScheme, AuthenticationSetup.OidcScheme);
        }

        await HttpContext.SignOutAsync(AuthenticationSetup.CookieScheme);
        return Redirect("/");
    }
}

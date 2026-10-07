using System.Net;
using CitadelIQ.Api.Authentication;
using CitadelIQ.Application.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AppAuthenticationOptions = CitadelIQ.Application.Options.AuthenticationOptions;

namespace CitadelIQ.Api.Controllers;

/// <summary>
/// <c>Authentication:Mode = DevelopmentLogin</c> only (startup refuses that mode outside Development): a local form
/// that signs in as any email, so the app can run without an Entra tenant. The same email is the same identity.
/// Returns 404 in every other mode.
/// </summary>
[AllowAnonymous]
[Route("auth/dev-login")]
public class DevLoginController(IOptions<AppAuthenticationOptions> options, IWebHostEnvironment environment, IAccountService accounts) : ControllerBase
{
    private bool Enabled => options.Value.Mode == AppAuthenticationOptions.DevelopmentLoginMode && environment.IsDevelopment();

    [HttpGet]
    public IActionResult Form([FromQuery] string? returnUrl)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        var target = WebUtility.HtmlEncode(SafeReturnUrl(returnUrl));
        var html = $$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>CitadelIQ — development sign-in</title>
            <style>body{font-family:system-ui,sans-serif;max-width:360px;margin:15vh auto;padding:0 16px}
            label{display:block;margin:12px 0 4px}input{width:100%;padding:8px;box-sizing:border-box}
            button{margin-top:16px;padding:8px 16px}p{color:#666;font-size:14px}</style></head>
            <body><h1>Development sign-in</h1>
            <p>For local development only. Any email signs in; the same email is the same account.</p>
            <form method="post" action="/auth/dev-login">
              <input type="hidden" name="returnUrl" value="{{target}}">
              <label for="email">Email</label><input id="email" name="email" type="email" required autofocus>
              <label for="displayName">Display name</label><input id="displayName" name="displayName" type="text">
              <button type="submit">Sign in</button>
            </form></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    public async Task<IActionResult> SignIn([FromForm] string? email, [FromForm] string? displayName, [FromForm] string? returnUrl)
    {
        if (!Enabled)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email is required.");
        }

        var subject = email.Trim().ToLowerInvariant();
        var user = await accounts.EnsureUserAsync(AuthenticationSetup.DevelopmentIssuer, subject, subject, displayName, HttpContext.RequestAborted);
        await HttpContext.SignInAsync(
            AuthenticationSetup.CookieScheme,
            SessionClaims.Create(AuthenticationSetup.DevelopmentIssuer, subject, user.DisplayName, user.Email));

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}

using CitadelIQ.Application.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using AppAuthenticationOptions = CitadelIQ.Application.Options.AuthenticationOptions;

namespace CitadelIQ.Api.Authentication;

/// <summary>
/// Backend-for-frontend sign-in: the API runs the OIDC authorization-code + PKCE flow against the identity provider
/// (Entra External ID) and keeps the session in an HttpOnly cookie. No tokens are stored anywhere — the ID token is
/// validated once at sign-in and replaced by the minimal <see cref="SessionClaims"/> principal.
/// </summary>
public static class AuthenticationSetup
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string OidcScheme = OpenIdConnectDefaults.AuthenticationScheme;
    public const string SignedInPolicy = "SignedIn";
    public const string DevelopmentIssuer = "urn:citadeliq:dev";

    public static void AddCitadelAuthentication(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Authentication").Get<AppAuthenticationOptions>() ?? new AppAuthenticationOptions();
        var useOidc = options.Mode == AppAuthenticationOptions.OidcMode;

        if (options.Mode == AppAuthenticationOptions.DevelopmentLoginMode && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Authentication:Mode 'DevelopmentLogin' is only allowed in the Development environment.");
        }

        var authentication = builder.Services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = CookieScheme;
                // Always the cookie: an unauthenticated API call must get a 401 the SPA can act on, never a 302 to the
                // identity provider (which fetch would follow cross-origin and fail). Sign-in is started explicitly
                // by AuthController.Login, which challenges the OIDC scheme by name.
                o.DefaultChallengeScheme = CookieScheme;
            })
            .AddCookie(CookieScheme, cookie =>
            {
                cookie.Cookie.Name = options.Session.CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.Path = "/";
                cookie.ExpireTimeSpan = TimeSpan.FromHours(options.Session.IdleTimeoutHours);
                cookie.SlidingExpiration = true;
                cookie.Events = new CookieAuthenticationEvents
                {
                    // An API never redirects to an HTML login page: the SPA handles 401 itself.
                    OnRedirectToLogin = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Please sign in."),
                    OnRedirectToAccessDenied = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Finish setting up your account to continue."),
                    // Sliding renewal re-issues the cookie, so the absolute lifetime is measured from the sign-in claim.
                    OnValidatePrincipal = async context =>
                    {
                        var signedInAt = context.Principal is null ? null : SessionClaims.GetSignedInAt(context.Principal);
                        if (signedInAt is null || DateTimeOffset.UtcNow - signedInAt > TimeSpan.FromDays(options.Session.AbsoluteLifetimeDays))
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieScheme);
                        }
                    }
                };
            });

        if (useOidc)
        {
            authentication.AddOpenIdConnect(OidcScheme, oidc =>
            {
                oidc.Authority = options.Oidc.Authority;
                oidc.ClientId = options.Oidc.ClientId;
                oidc.ClientSecret = options.Oidc.ClientSecret;
                oidc.ResponseType = "code";
                oidc.UsePkce = true;
                oidc.SaveTokens = false;
                oidc.MapInboundClaims = false;
                oidc.GetClaimsFromUserInfoEndpoint = false;
                oidc.SignedOutRedirectUri = "/";
                oidc.Scope.Clear();
                foreach (var scope in options.Oidc.Scopes)
                {
                    oidc.Scope.Add(scope);
                }

                oidc.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        if (!SessionClaims.TryGetIdentity(principal, out var issuer, out var subject))
                        {
                            context.Fail("The identity token has no issuer/subject.");
                            return;
                        }

                        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value;
                        var name = principal.FindFirst("name")?.Value;
                        var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                        var user = await accounts.EnsureUserAsync(issuer, subject, email, name, context.HttpContext.RequestAborted);

                        context.Principal = SessionClaims.Create(issuer, subject, user.DisplayName, user.Email);
                    },
                    OnRemoteFailure = context =>
                    {
                        // Never show the identity provider's error to the user.
                        context.Response.Redirect("/?signin=failed");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    }
                };
            });
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuthorizationHandler, ActiveMemberHandler>();
        builder.Services.AddAuthorization(o =>
        {
            o.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ActiveMemberRequirement())
                .Build();
            o.AddPolicy(SignedInPolicy, p => p.RequireAuthenticatedUser());
        });
    }
}

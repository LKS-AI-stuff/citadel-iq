using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/settings")]
[AllowAnonymous] // client-safe limits, loaded before sign-in
public class SettingsController(IAppSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public ActionResult<AppSettingsDto> Get() => Ok(settingsService.GetSettings());
}

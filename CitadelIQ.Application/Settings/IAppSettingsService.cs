using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Settings;

public interface IAppSettingsService
{
    AppSettingsDto GetSettings();
}

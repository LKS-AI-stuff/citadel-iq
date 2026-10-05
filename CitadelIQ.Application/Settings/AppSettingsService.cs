using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Settings;

public class AppSettingsService(IOptions<RagOptions> options) : IAppSettingsService
{
    public AppSettingsDto GetSettings()
    {
        var rag = options.Value;
        return new AppSettingsDto(rag.Enabled, rag.MaxQuestionLength, rag.MaxHistoryTurns, rag.MaxHistoryChars);
    }
}

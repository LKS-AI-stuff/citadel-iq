using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Application.Accounts;

/// <summary>Resolves "uploaded by" display info for a batch of documents in one query.</summary>
public class UploaderLookup(IUserAccountRepository userRepository, ICurrentUser currentUser)
{
    public async Task<IReadOnlyDictionary<Guid, UploaderDto>> GetAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UploaderDto>();
        }

        var infos = await userRepository.GetDisplayInfoAsync(ids, cancellationToken);
        var workspaceId = currentUser.WorkspaceId;

        return infos.ToDictionary(
            i => i.UserId,
            i => new UploaderDto(i.DisplayName, IsFormerMember: i.IsClosed || i.WorkspaceId != workspaceId));
    }

    public static UploaderDto? Find(IReadOnlyDictionary<Guid, UploaderDto> uploaders, Guid? userId) =>
        userId is { } id && uploaders.TryGetValue(id, out var uploader) ? uploader : null;
}

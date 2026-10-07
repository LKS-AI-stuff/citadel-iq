using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Domain.Exceptions;

namespace CitadelIQ.Domain.Rules;

/// <summary>Organization membership rules. Callers apply them to the full, locked membership set of a workspace.</summary>
public static class MembershipRules
{
    public const string LastOwnerMessage = "An organization must always have at least one Owner.";

    /// <summary>Throws unless at least one Owner remains (every Owner is also an Admin, so this also keeps an Admin).</summary>
    public static void EnsureOwnerRemains(IEnumerable<Membership> memberships)
    {
        if (!memberships.Any(m => m.Role == WorkspaceRole.Owner))
        {
            throw new DomainException(LastOwnerMessage);
        }
    }

    /// <summary>
    /// Whether <paramref name="actorRole"/> may change a member whose role is <paramref name="targetCurrentRole"/>
    /// (to <paramref name="targetNewRole"/>, or remove them when null). Admins manage Members and Admins; only Owners
    /// touch Owners — including promoting someone to Owner.
    /// </summary>
    public static bool CanManage(WorkspaceRole actorRole, WorkspaceRole targetCurrentRole, WorkspaceRole? targetNewRole)
    {
        if (actorRole < WorkspaceRole.Admin)
        {
            return false;
        }

        if (actorRole == WorkspaceRole.Owner)
        {
            return true;
        }

        return targetCurrentRole != WorkspaceRole.Owner && targetNewRole != WorkspaceRole.Owner;
    }
}

namespace CitadelIQ.Domain.Enums;

public enum SearchScope
{
    /// <summary>The caller's whole workspace, from its root folder down.</summary>
    EntireWorkspace,
    CurrentFolder,
    CurrentFolderAndSubfolders
}

namespace Hospital.BLL.Helpers;

/// <summary>Centralized, fail-closed ownership checks for user-owned resources.</summary>
public static class ResourceOwnership
{
    public static bool IsOwner(string? authenticatedUserId, string? resourceOwnerId) =>
        !string.IsNullOrWhiteSpace(authenticatedUserId) &&
        !string.IsNullOrWhiteSpace(resourceOwnerId) &&
        string.Equals(authenticatedUserId, resourceOwnerId, StringComparison.Ordinal);
}

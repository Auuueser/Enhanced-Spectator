namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Contains pure safety rules for local vanilla connected-player state repair.
/// </summary>
public static class ConnectedPlayerStateRepairRules
{
    /// <summary>
    /// Gets whether a connected non-local vanilla player slot should be restored to controlled.
    /// </summary>
    public static bool ShouldRestoreConnectedAliveControl(
        bool isLocalPlayer,
        bool isCurrentlyConnected,
        bool isPlayerControlled,
        bool isPlayerDead)
    {
        return !isLocalPlayer
            && isCurrentlyConnected
            && !isPlayerControlled
            && !isPlayerDead;
    }

    /// <summary>
    /// Gets whether a connected non-local vanilla player slot should clear stale disconnect state.
    /// </summary>
    public static bool ShouldClearDisconnectedMidGame(
        bool isLocalPlayer,
        bool isCurrentlyConnected,
        bool disconnectedMidGame)
    {
        return !isLocalPlayer
            && isCurrentlyConnected
            && disconnectedMidGame;
    }

    /// <summary>
    /// Gets whether a vanilla fallback display name should replace a generic player-number label.
    /// </summary>
    public static bool ShouldUseVanillaFallbackDisplayName(
        bool updatePlayerNames,
        bool hasModIdentityDisplayName,
        string? currentDisplayName)
    {
        return updatePlayerNames
            && !hasModIdentityDisplayName
            && !string.IsNullOrWhiteSpace(currentDisplayName)
            && PlayerDisplayNameRules.IsGenericPlayerNumber(currentDisplayName);
    }

    /// <summary>
    /// Gets whether a peer's synced name should be written over the current one. A name that differs only in white
    /// space is left alone so the name repairs keep their spacing. With LC Chinese Project installed, its name
    /// management captures and applies real names, so the synced name only fills empty or generic Player #n slots.
    /// </summary>
    public static bool ShouldWriteIdentityDisplayName(string? currentDisplayName, string identityDisplayName, bool chineseProjectNames)
    {
        if (PlayerDisplayNameRules.SameIgnoringWhitespace(currentDisplayName, identityDisplayName)) return false;
        return !chineseProjectNames || string.IsNullOrWhiteSpace(currentDisplayName)
            || PlayerDisplayNameRules.IsGenericPlayerNumber(currentDisplayName!);
    }
}

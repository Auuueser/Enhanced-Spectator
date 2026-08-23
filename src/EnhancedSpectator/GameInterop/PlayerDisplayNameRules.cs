using System;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Normalizes runtime player display names before they are shown by local-only visuals.
/// </summary>
public static class PlayerDisplayNameRules
{
    private const string PlayerNumberPrefix = "Player#";

    /// <summary>
    /// Attempts to normalize a game-provided player name.
    /// </summary>
    public static bool TryNormalize(string? playerUsername, out string displayName)
    {
        if (string.IsNullOrWhiteSpace(playerUsername))
        {
            displayName = string.Empty;
            return false;
        }

        string trimmed = playerUsername.Trim();
        if (IsGenericPlayerNumber(trimmed))
        {
            displayName = string.Empty;
            return false;
        }

        displayName = trimmed;
        return displayName.Length > 0;
    }

    /// <summary>
    /// Resolves a display name using an independent authoritative value to remove only proven synthetic digit suffixes.
    /// </summary>
    public static bool TryResolve(
        string? playerUsername,
        string? authoritativeName,
        out string displayName)
    {
        bool hasPlayerName = TryNormalize(playerUsername, out string normalizedPlayerName);
        bool hasAuthoritativeName = TryNormalize(authoritativeName, out string normalizedAuthoritativeName);

        if (hasAuthoritativeName
            && (!hasPlayerName
                || string.Equals(normalizedPlayerName, normalizedAuthoritativeName, StringComparison.Ordinal)
                || HasSyntheticNumericSuffix(normalizedPlayerName, normalizedAuthoritativeName)))
        {
            displayName = normalizedAuthoritativeName;
            return true;
        }

        if (hasPlayerName)
        {
            displayName = normalizedPlayerName;
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    /// <summary>
    /// Gets whether a display name is the game's generic Player #n placeholder.
    /// </summary>
    public static bool IsGenericPlayerNumber(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        string compact = RemoveWhitespace(displayName);
        if (!compact.StartsWith(PlayerNumberPrefix, StringComparison.OrdinalIgnoreCase)
            || compact.Length == PlayerNumberPrefix.Length)
        {
            return false;
        }

        for (int index = PlayerNumberPrefix.Length; index < compact.Length; index++)
        {
            if (!char.IsDigit(compact[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets whether a displayed name is exactly an independently known authoritative name plus digits.
    /// </summary>
    public static bool HasSyntheticNumericSuffix(string displayedName, string authoritativeName)
    {
        if (string.IsNullOrEmpty(displayedName)
            || string.IsNullOrEmpty(authoritativeName)
            || displayedName.Length <= authoritativeName.Length
            || !displayedName.StartsWith(authoritativeName, StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = authoritativeName.Length; index < displayedName.Length; index++)
        {
            if (!char.IsDigit(displayedName[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string RemoveWhitespace(string value)
    {
        char[] buffer = new char[value.Length];
        int writeIndex = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!char.IsWhiteSpace(current))
            {
                buffer[writeIndex] = current;
                writeIndex++;
            }
        }

        return new string(buffer, 0, writeIndex);
    }
}

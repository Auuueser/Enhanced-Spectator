using System;
using System.Text;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>
/// Pure validation and render rules for host-gated, player-owned fear visuals.
/// </summary>
public static class FearModeRules
{
    /// <summary>Model key that requests the normal ghost-head fallback.</summary>
    public const string DefaultModelKey = "Default";

    /// <summary>Maximum accepted model-key character count.</summary>
    public const int MaxModelKeyCharacters = 48;

    /// <summary>Maximum UTF-8 bytes accepted by FixedString64 serialization.</summary>
    public const int MaxModelKeyUtf8Bytes = 60;

    /// <summary>Gets whether a model key is bounded and contains no control characters.</summary>
    public static bool IsValidModelKey(string? modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey)
            || modelKey.Length > MaxModelKeyCharacters
            || Encoding.UTF8.GetByteCount(modelKey) > MaxModelKeyUtf8Bytes)
        {
            return false;
        }

        for (int index = 0; index < modelKey.Length; index++)
        {
            if (char.IsControl(modelKey[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets whether the host may accept a player-owned selection packet.</summary>
    public static bool CanHostAcceptSelection(
        bool isHost,
        bool sessionEnabled,
        ulong senderClientId,
        ulong originClientId,
        bool originIsDead,
        bool modelIsAllowed)
    {
        return isHost
            && sessionEnabled
            && senderClientId == originClientId
            && originIsDead
            && modelIsAllowed;
    }

    /// <summary>Gets whether this viewer should replace the default head with a fear visual.</summary>
    public static bool ShouldRenderFearVisual(
        bool sessionEnabled,
        bool localRenderingEnabled,
        FearModeSelectionState? selection,
        bool sourceAvailable)
    {
        return sessionEnabled
            && localRenderingEnabled
            && selection != null
            && !string.Equals(selection.ModelKey, DefaultModelKey, StringComparison.Ordinal)
            && sourceAvailable;
    }
}

using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Shares local freecam input suppression state with spectator patches.
/// </summary>
public static class SpectatorVanillaInputGuard
{
    private static bool _freecamWantsVerticalInput;
    private static KeyCode _ascendKey = KeyCode.None;
    private static KeyCode _descendKey = KeyCode.None;
    private static bool _quickMenuBlocksInput;
    private static int _internalTargetSwitchDepth;

    /// <summary>
    /// Updates the current input state that should suppress vanilla spectator controls.
    /// </summary>
    public static void Update(
        bool freecamEnabled,
        KeyCode ascendKey,
        KeyCode descendKey,
        bool quickMenuBlocksInput)
    {
        _freecamWantsVerticalInput = freecamEnabled;
        _ascendKey = ascendKey;
        _descendKey = descendKey;
        _quickMenuBlocksInput = quickMenuBlocksInput;
    }

    /// <summary>
    /// Clears all vanilla input suppression state.
    /// </summary>
    public static void Clear()
    {
        _freecamWantsVerticalInput = false;
        _ascendKey = KeyCode.None;
        _descendKey = KeyCode.None;
        _quickMenuBlocksInput = false;
        _internalTargetSwitchDepth = 0;
    }

    /// <summary>
    /// Marks a mod-owned vanilla target switch that should not be suppressed as local input.
    /// </summary>
    public static void BeginInternalTargetSwitch()
    {
        _internalTargetSwitchDepth++;
    }

    /// <summary>
    /// Clears a mod-owned vanilla target switch scope.
    /// </summary>
    public static void EndInternalTargetSwitch()
    {
        if (_internalTargetSwitchDepth > 0)
        {
            _internalTargetSwitchDepth--;
        }
    }

    /// <summary>
    /// Gets whether vanilla target switching should be suppressed for the current frame.
    /// </summary>
    public static bool ShouldSuppressTargetSwitchInput(out string reason)
    {
        bool ascendKeyHeld = false;
        bool descendKeyHeld = false;
        if (_freecamWantsVerticalInput && _internalTargetSwitchDepth <= 0)
        {
            ascendKeyHeld = SpectatorInputService.IsKeyHeld(_ascendKey);
            descendKeyHeld = SpectatorInputService.IsKeyHeld(_descendKey);
        }

        return SpectatorVanillaInputGuardRules.ShouldSuppressTargetSwitchInput(
            _internalTargetSwitchDepth > 0,
            _freecamWantsVerticalInput,
            ascendKeyHeld,
            descendKeyHeld,
            out reason);
    }

    /// <summary>
    /// Gets whether local gameplay interaction input should be suppressed for the current frame.
    /// </summary>
    public static bool ShouldSuppressGameplayInteractInput()
    {
        return _quickMenuBlocksInput;
    }
}

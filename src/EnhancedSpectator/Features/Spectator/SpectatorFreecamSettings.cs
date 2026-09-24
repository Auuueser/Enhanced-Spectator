using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Provides live access to spectator freecam configuration values.
/// </summary>
public sealed class SpectatorFreecamSettings
{
    private readonly EnhancedSpectatorConfig _config;

    /// <summary>Local camera controls.</summary>
    public SpectatorCameraConfig Camera => _config.Camera;

    /// <summary>Persists live third-person distance.</summary>
    public void SetThirdPersonDistance(float value) => _config.ThirdPersonDistance.Value = value;

    /// <summary>
    /// Creates a settings facade over BepInEx config entries.
    /// </summary>
    public SpectatorFreecamSettings(EnhancedSpectatorConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Gets whether all enhanced spectator behavior is enabled.
    /// </summary>
    public bool EnableEnhancedSpectator => _config.EnableEnhancedSpectator.Value;

    /// <summary>
    /// Gets whether local freecam behavior is enabled.
    /// </summary>
    public bool EnableFreecam => _config.EnableFreecam.Value;

    /// <summary>
    /// Gets whether freecam should enable automatically when entering spectator state.
    /// </summary>
    public bool FreecamDefaultOn => _config.FreecamDefaultOn.Value;

    /// <summary>
    /// Gets the maximum camera offset radius from the target anchor.
    /// </summary>
    public float FreecamRadius => Mathf.Max(0f, _config.FreecamRadius.Value);

    /// <summary>
    /// Gets the base movement speed in units per second.
    /// </summary>
    public float FreecamMoveSpeed => Mathf.Max(0f, _config.FreecamMoveSpeed.Value);

    /// <summary>
    /// Gets the multiplier applied while the fast key is held.
    /// </summary>
    public float FreecamFastMoveMultiplier => Mathf.Max(0f, _config.FreecamFastMoveMultiplier.Value);

    /// <summary>
    /// Gets the multiplier applied while the slow key is held.
    /// </summary>
    public float FreecamSlowMoveMultiplier => Mathf.Max(0f, _config.FreecamSlowMoveMultiplier.Value);

    /// <summary>
    /// Gets the mouse look sensitivity multiplier.
    /// </summary>
    public float FreecamLookSensitivity => Mathf.Max(0f, _config.FreecamLookSensitivity.Value);

    /// <summary>
    /// Gets the smooth damp time. Zero disables smoothing.
    /// </summary>
    public float FreecamSmoothTime => Mathf.Max(0f, _config.FreecamSmoothTime.Value);

    /// <summary>
    /// Gets whether the camera offset should be clamped to the configured radius.
    /// </summary>
    public bool ClampCameraToRadius => _config.ClampCameraToRadius.Value;

    /// <summary>
    /// Gets whether target switches should recenter the freecam.
    /// </summary>
    public bool RecenterOnTargetSwitch => _config.RecenterOnTargetSwitch.Value;

    /// <summary>
    /// Gets whether freecam should disable during vanilla game-over override.
    /// </summary>
    public bool DisableDuringGameOverOverride => _config.DisableDuringGameOverOverride.Value;

    /// <summary>
    /// Gets whether self-ghost third-person view is enabled.
    /// </summary>
    public bool EnableThirdPerson => _config.EnableThirdPerson.Value;

    /// <summary>
    /// Gets the trailing third-person camera distance.
    /// </summary>
    public float ThirdPersonDistance => Mathf.Max(0.25f, _config.ThirdPersonDistance.Value);

    /// <summary>
    /// Gets the third-person camera height offset.
    /// </summary>
    public float ThirdPersonHeight => _config.ThirdPersonHeight.Value;

    /// <summary>
    /// Gets the key that toggles enhanced freecam.
    /// </summary>
    public KeyCode ToggleFreecamKey => _config.ToggleFreecamKey.Value;

    /// <summary>
    /// Gets the key that recenters the freecam near the current target.
    /// </summary>
    public KeyCode RecenterKey => _config.RecenterKey.Value;

    /// <summary>
    /// Gets the key that disables enhanced freecam until toggled again.
    /// </summary>
    public KeyCode ResetToVanillaViewKey => _config.ResetToVanillaViewKey.Value;

    /// <summary>
    /// Gets the key that toggles self-ghost third-person view.
    /// </summary>
    public KeyCode ToggleThirdPersonKey => _config.ToggleThirdPersonKey.Value;

    /// <summary>
    /// Gets the fast movement modifier key.
    /// </summary>
    public KeyCode FastMoveKey => _config.FastMoveKey.Value;

    /// <summary>
    /// Gets the slow movement modifier key.
    /// </summary>
    public KeyCode SlowMoveKey => _config.SlowMoveKey.Value;

    /// <summary>
    /// Gets the key that moves the freecam upward.
    /// </summary>
    public KeyCode AscendKey => _config.AscendKey.Value;

    /// <summary>
    /// Gets the key that moves the freecam downward.
    /// </summary>
    public KeyCode DescendKey => _config.DescendKey.Value;
}

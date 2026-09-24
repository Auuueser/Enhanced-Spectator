using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputKey = UnityEngine.InputSystem.Key;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Reads local Unity Input System controls for spectator freecam input.
/// </summary>
public sealed class SpectatorInputService
{
    private const float MouseDeltaScale = 0.05f;

    private readonly SpectatorFreecamSettings _settings;
    private readonly SpectatorInputKeyCache _toggleFreecamKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _recenterKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _resetToVanillaViewKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _toggleThirdPersonKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _fastMoveKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _slowMoveKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _ascendKey = new SpectatorInputKeyCache();
    private readonly SpectatorInputKeyCache _descendKey = new SpectatorInputKeyCache();

    /// <summary>
    /// Creates an input service for spectator freecam controls.
    /// </summary>
    public SpectatorInputService(SpectatorFreecamSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets whether the freecam toggle key was pressed this frame.
    /// </summary>
    public bool ToggleFreecamPressed => GetConfiguredKeyDown(_toggleFreecamKey, _settings.ToggleFreecamKey);

    /// <summary>
    /// Gets whether the recenter key was pressed this frame.
    /// </summary>
    public bool RecenterPressed => GetConfiguredKeyDown(_recenterKey, _settings.RecenterKey);

    /// <summary>
    /// Gets whether the reset-to-vanilla key was pressed this frame.
    /// </summary>
    public bool ResetToVanillaPressed => GetConfiguredKeyDown(_resetToVanillaViewKey, _settings.ResetToVanillaViewKey);

    /// <summary>
    /// Gets whether the self-ghost third-person toggle key was pressed this frame.
    /// </summary>
    public bool ToggleThirdPersonPressed =>
        GetConfiguredKeyDown(_toggleThirdPersonKey, _settings.ToggleThirdPersonKey);

    /// <summary>
    /// Gets whether the fast movement key is currently held.
    /// </summary>
    public bool FastMoveHeld => GetConfiguredKey(_fastMoveKey, _settings.FastMoveKey);

    /// <summary>
    /// Gets whether the slow movement key is currently held.
    /// </summary>
    public bool SlowMoveHeld => GetConfiguredKey(_slowMoveKey, _settings.SlowMoveKey);

    /// <summary>
    /// Gets whether the configured ascend key is currently held.
    /// </summary>
    public bool AscendHeld => GetConfiguredKey(_ascendKey, _settings.AscendKey);

    /// <summary>
    /// Gets whether the configured descend key is currently held.
    /// </summary>
    public bool DescendHeld => GetConfiguredKey(_descendKey, _settings.DescendKey);

    /// <summary>
    /// Reads configured horizontal and vertical movement input.
    /// </summary>
    public Vector3 ReadMoveInput()
    {
        Keyboard? keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector3.zero;
        }

        float x = 0f;
        float y = 0f;
        float z = 0f;

        if (IsKeyHeld(_settings.Camera.MoveLeftKey.Value))
        {
            x -= 1f;
        }

        if (IsKeyHeld(_settings.Camera.MoveRightKey.Value))
        {
            x += 1f;
        }

        if (DescendHeld)
        {
            y -= 1f;
        }

        if (AscendHeld)
        {
            y += 1f;
        }

        if (IsKeyHeld(_settings.Camera.MoveBackKey.Value))
        {
            z -= 1f;
        }

        if (IsKeyHeld(_settings.Camera.MoveForwardKey.Value))
        {
            z += 1f;
        }

        Vector3 move = new Vector3(x, y, z);
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    /// <summary>
    /// Reads mouse look delta from Unity Input System.
    /// </summary>
    public Vector2 ReadLookDelta()
    {
        Mouse? mouse = Mouse.current;
        return mouse == null ? Vector2.zero : mouse.delta.ReadValue() * MouseDeltaScale;
    }

    /// <summary>Reads the current mouse-wheel delta.</summary>
    public float ReadScrollDelta()
    {
        Mouse? mouse = Mouse.current;
        return mouse == null ? 0f : mouse.scroll.ReadValue().y / 120f;
    }

    /// <summary>
    /// Reads a configured key from Unity Input System.
    /// </summary>
    public static bool IsKeyHeld(KeyCode key)
    {
        return GetKey(key) || GetKey(OtherModifier(key));
    }

    /// <summary>
    /// Reads whether a configured key was pressed during this frame.
    /// </summary>
    public static bool IsKeyPressedThisFrame(KeyCode key)
    {
        return GetKeyDown(key) || GetKeyDown(OtherModifier(key));
    }

    private static KeyCode OtherModifier(KeyCode key) => key switch
    {
        KeyCode.LeftAlt => KeyCode.RightAlt, KeyCode.RightAlt => KeyCode.LeftAlt,
        KeyCode.LeftControl => KeyCode.RightControl, KeyCode.RightControl => KeyCode.LeftControl,
        KeyCode.LeftShift => KeyCode.RightShift, KeyCode.RightShift => KeyCode.LeftShift,
        _ => KeyCode.None
    };

    private static bool GetKey(KeyCode key)
    {
        if (!TryGetInputSystemKey(key, out InputKey inputKey))
        {
            return false;
        }

        Keyboard? keyboard = Keyboard.current;
        return keyboard != null && IsPressed(keyboard, inputKey);
    }

    private static bool GetKeyDown(KeyCode key)
    {
        if (!TryGetInputSystemKey(key, out InputKey inputKey))
        {
            return false;
        }

        Keyboard? keyboard = Keyboard.current;
        return keyboard != null && WasPressedThisFrame(keyboard, inputKey);
    }

    private static bool GetConfiguredKey(SpectatorInputKeyCache cache, KeyCode key)
    {
        if (!cache.TryResolve(key, out InputKey inputKey))
        {
            return false;
        }

        Keyboard? keyboard = Keyboard.current;
        return keyboard != null && (IsPressed(keyboard, inputKey) || GetKey(OtherModifier(key)));
    }

    private static bool GetConfiguredKeyDown(SpectatorInputKeyCache cache, KeyCode key)
    {
        if (!cache.TryResolve(key, out InputKey inputKey))
        {
            return false;
        }

        Keyboard? keyboard = Keyboard.current;
        return keyboard != null && (WasPressedThisFrame(keyboard, inputKey) || GetKeyDown(OtherModifier(key)));
    }

    private static bool IsPressed(Keyboard keyboard, InputKey key)
    {
        KeyControl control = keyboard[key];
        return control != null && control.isPressed;
    }

    private static bool WasPressedThisFrame(Keyboard keyboard, InputKey key)
    {
        KeyControl control = keyboard[key];
        return control != null && control.wasPressedThisFrame;
    }

    private static bool TryGetInputSystemKey(KeyCode keyCode, out InputKey inputKey)
    {
        return SpectatorInputKeyMappings.TryResolve(keyCode, out inputKey);
    }
}

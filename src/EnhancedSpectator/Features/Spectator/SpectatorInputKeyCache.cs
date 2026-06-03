using System;
using UnityEngine;
using InputKey = UnityEngine.InputSystem.Key;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Stores the result of mapping one configured Unity key into an Input System key.
/// </summary>
public readonly struct SpectatorInputKeyResolution
{
    /// <summary>
    /// Creates a key mapping result.
    /// </summary>
    public SpectatorInputKeyResolution(bool isMapped, InputKey inputKey)
    {
        IsMapped = isMapped;
        InputKey = inputKey;
    }

    /// <summary>
    /// Gets an unmapped key result.
    /// </summary>
    public static SpectatorInputKeyResolution Unmapped => new SpectatorInputKeyResolution(false, InputKey.None);

    /// <summary>
    /// Gets whether the configured key can be read through the Input System keyboard.
    /// </summary>
    public bool IsMapped { get; }

    /// <summary>
    /// Gets the mapped Input System key, or <see cref="InputKey.None"/> when unmapped.
    /// </summary>
    public InputKey InputKey { get; }
}

/// <summary>
/// Caches key mapping for a single configurable input binding.
/// </summary>
public sealed class SpectatorInputKeyCache
{
    private readonly Func<KeyCode, SpectatorInputKeyResolution> _resolve;
    private KeyCode _keyCode;
    private SpectatorInputKeyResolution _resolution;
    private bool _hasResolution;

    /// <summary>
    /// Creates a cache backed by the default Enhanced Spectator key mapping table.
    /// </summary>
    public SpectatorInputKeyCache()
        : this(SpectatorInputKeyMappings.Resolve)
    {
    }

    /// <summary>
    /// Creates a cache backed by a custom mapper.
    /// </summary>
    public SpectatorInputKeyCache(Func<KeyCode, SpectatorInputKeyResolution> resolve)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _keyCode = KeyCode.None;
        _resolution = SpectatorInputKeyResolution.Unmapped;
    }

    /// <summary>
    /// Resolves the configured key, remapping only when the configured key changed.
    /// </summary>
    public bool TryResolve(KeyCode keyCode, out InputKey inputKey)
    {
        if (!_hasResolution || _keyCode != keyCode)
        {
            _keyCode = keyCode;
            _resolution = _resolve(keyCode);
            _hasResolution = true;
        }

        inputKey = _resolution.InputKey;
        return _resolution.IsMapped;
    }

    /// <summary>
    /// Clears the cached key mapping.
    /// </summary>
    public void Clear()
    {
        _keyCode = KeyCode.None;
        _resolution = SpectatorInputKeyResolution.Unmapped;
        _hasResolution = false;
    }
}

/// <summary>
/// Maps Unity legacy key codes to Unity Input System keyboard keys.
/// </summary>
public static class SpectatorInputKeyMappings
{
    /// <summary>
    /// Resolves a Unity key code into an Input System key.
    /// </summary>
    public static SpectatorInputKeyResolution Resolve(KeyCode keyCode)
    {
        InputKey inputKey = keyCode switch
        {
            KeyCode.Backspace => InputKey.Backspace,
            KeyCode.Tab => InputKey.Tab,
            KeyCode.Return => InputKey.Enter,
            KeyCode.Escape => InputKey.Escape,
            KeyCode.Space => InputKey.Space,
            KeyCode.Quote => InputKey.Quote,
            KeyCode.Comma => InputKey.Comma,
            KeyCode.Minus => InputKey.Minus,
            KeyCode.Period => InputKey.Period,
            KeyCode.Slash => InputKey.Slash,
            KeyCode.Alpha0 => InputKey.Digit0,
            KeyCode.Alpha1 => InputKey.Digit1,
            KeyCode.Alpha2 => InputKey.Digit2,
            KeyCode.Alpha3 => InputKey.Digit3,
            KeyCode.Alpha4 => InputKey.Digit4,
            KeyCode.Alpha5 => InputKey.Digit5,
            KeyCode.Alpha6 => InputKey.Digit6,
            KeyCode.Alpha7 => InputKey.Digit7,
            KeyCode.Alpha8 => InputKey.Digit8,
            KeyCode.Alpha9 => InputKey.Digit9,
            KeyCode.Semicolon => InputKey.Semicolon,
            KeyCode.Equals => InputKey.Equals,
            KeyCode.LeftBracket => InputKey.LeftBracket,
            KeyCode.Backslash => InputKey.Backslash,
            KeyCode.RightBracket => InputKey.RightBracket,
            KeyCode.BackQuote => InputKey.Backquote,
            KeyCode.A => InputKey.A,
            KeyCode.B => InputKey.B,
            KeyCode.C => InputKey.C,
            KeyCode.D => InputKey.D,
            KeyCode.E => InputKey.E,
            KeyCode.F => InputKey.F,
            KeyCode.G => InputKey.G,
            KeyCode.H => InputKey.H,
            KeyCode.I => InputKey.I,
            KeyCode.J => InputKey.J,
            KeyCode.K => InputKey.K,
            KeyCode.L => InputKey.L,
            KeyCode.M => InputKey.M,
            KeyCode.N => InputKey.N,
            KeyCode.O => InputKey.O,
            KeyCode.P => InputKey.P,
            KeyCode.Q => InputKey.Q,
            KeyCode.R => InputKey.R,
            KeyCode.S => InputKey.S,
            KeyCode.T => InputKey.T,
            KeyCode.U => InputKey.U,
            KeyCode.V => InputKey.V,
            KeyCode.W => InputKey.W,
            KeyCode.X => InputKey.X,
            KeyCode.Y => InputKey.Y,
            KeyCode.Z => InputKey.Z,
            KeyCode.Delete => InputKey.Delete,
            KeyCode.Keypad0 => InputKey.Numpad0,
            KeyCode.Keypad1 => InputKey.Numpad1,
            KeyCode.Keypad2 => InputKey.Numpad2,
            KeyCode.Keypad3 => InputKey.Numpad3,
            KeyCode.Keypad4 => InputKey.Numpad4,
            KeyCode.Keypad5 => InputKey.Numpad5,
            KeyCode.Keypad6 => InputKey.Numpad6,
            KeyCode.Keypad7 => InputKey.Numpad7,
            KeyCode.Keypad8 => InputKey.Numpad8,
            KeyCode.Keypad9 => InputKey.Numpad9,
            KeyCode.KeypadPeriod => InputKey.NumpadPeriod,
            KeyCode.KeypadDivide => InputKey.NumpadDivide,
            KeyCode.KeypadMultiply => InputKey.NumpadMultiply,
            KeyCode.KeypadMinus => InputKey.NumpadMinus,
            KeyCode.KeypadPlus => InputKey.NumpadPlus,
            KeyCode.KeypadEnter => InputKey.NumpadEnter,
            KeyCode.KeypadEquals => InputKey.NumpadEquals,
            KeyCode.UpArrow => InputKey.UpArrow,
            KeyCode.DownArrow => InputKey.DownArrow,
            KeyCode.RightArrow => InputKey.RightArrow,
            KeyCode.LeftArrow => InputKey.LeftArrow,
            KeyCode.Insert => InputKey.Insert,
            KeyCode.Home => InputKey.Home,
            KeyCode.End => InputKey.End,
            KeyCode.PageUp => InputKey.PageUp,
            KeyCode.PageDown => InputKey.PageDown,
            KeyCode.F1 => InputKey.F1,
            KeyCode.F2 => InputKey.F2,
            KeyCode.F3 => InputKey.F3,
            KeyCode.F4 => InputKey.F4,
            KeyCode.F5 => InputKey.F5,
            KeyCode.F6 => InputKey.F6,
            KeyCode.F7 => InputKey.F7,
            KeyCode.F8 => InputKey.F8,
            KeyCode.F9 => InputKey.F9,
            KeyCode.F10 => InputKey.F10,
            KeyCode.F11 => InputKey.F11,
            KeyCode.F12 => InputKey.F12,
            KeyCode.Numlock => InputKey.NumLock,
            KeyCode.CapsLock => InputKey.CapsLock,
            KeyCode.ScrollLock => InputKey.ScrollLock,
            KeyCode.RightShift => InputKey.RightShift,
            KeyCode.LeftShift => InputKey.LeftShift,
            KeyCode.RightControl => InputKey.RightCtrl,
            KeyCode.LeftControl => InputKey.LeftCtrl,
            KeyCode.RightAlt => InputKey.RightAlt,
            KeyCode.LeftAlt => InputKey.LeftAlt,
            _ => InputKey.None,
        };

        return inputKey == InputKey.None
            ? SpectatorInputKeyResolution.Unmapped
            : new SpectatorInputKeyResolution(true, inputKey);
    }

    /// <summary>
    /// Tries to resolve a Unity key code into an Input System key.
    /// </summary>
    public static bool TryResolve(KeyCode keyCode, out InputKey inputKey)
    {
        SpectatorInputKeyResolution resolution = Resolve(keyCode);
        inputKey = resolution.InputKey;
        return resolution.IsMapped;
    }
}

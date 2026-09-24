using System;

namespace EnhancedSpectator.GameInterop;

/// <summary>Restores only the last write to the exact same UI binding, never a replacement HUD's value.</summary>
internal sealed class PlayerNameOwnedWrite
{
    private object? _target;
    private Func<string?>? _read;
    private Action<string>? _write;
    private string _original = "", _expected = "";

    internal bool Owns(object target, string? value) => ReferenceEquals(_target, target)
        && string.Equals(value, _expected, StringComparison.Ordinal);

    internal void Apply(object target, Func<string?> read, Action<string> write, string desired, bool recognized)
    {
        string? current = read();
        if (current == null || current == desired) return;
        bool owned = Owns(target, current);
        if (!recognized && !owned) return;
        if (!ReferenceEquals(_target, target)) { Restore(); _original = current; }
        else if (!owned) _original = current;
        _target = target; _read = read; _write = write; _expected = desired;
        write(desired);
    }

    internal void Restore()
    {
        if (_read != null && string.Equals(_read(), _expected, StringComparison.Ordinal)) _write!(_original);
        _target = null; _read = null; _write = null;
    }
}

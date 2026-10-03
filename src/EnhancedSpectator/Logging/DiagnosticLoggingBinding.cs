using System;
using BepInEx.Configuration;

namespace EnhancedSpectator.Logging;

internal sealed class DiagnosticLoggingBinding : IDisposable
{
    private readonly ConfigEntry<bool> _entry;
    private readonly Action _onEnabled;
    internal DiagnosticLoggingBinding(ConfigEntry<bool> entry, Action onEnabled)
    {
        _entry = entry; _onEnabled = onEnabled;
        _entry.SettingChanged += Changed;
        Apply();
    }
    private void Changed(object sender, EventArgs args) => Apply();
    private void Apply()
    {
        ModLog.SetDebugEnabled(_entry.Value);
        if (_entry.Value) _onEnabled();
    }
    public void Dispose()
    {
        _entry.SettingChanged -= Changed;
        ModLog.SetDebugEnabled(false);
    }
}

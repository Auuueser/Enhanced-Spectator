using System.Collections.Generic;

namespace EnhancedSpectator.GameInterop;

// The borrowed bit belongs to this camera's working frame settings, never its saved settings.
internal sealed class NativeFadeCameraGate
{
    private readonly HashSet<int> _borrowed = new HashSet<int>();
    private int _frame = -1;
    internal bool Prepare(int frame, int camera, bool requested, bool originallyEnabled)
    {
        if (_frame != frame) { _borrowed.Clear(); _frame = frame; }
        if (requested && !originallyEnabled) _borrowed.Add(camera);
        else _borrowed.Remove(camera);
        return originallyEnabled || requested;
    }
    internal bool SuppressOtherPass(int frame, int camera, bool ownedPass) =>
        frame == _frame && !ownedPass && _borrowed.Contains(camera);
    internal void Clear() { _borrowed.Clear(); _frame = -1; }
}

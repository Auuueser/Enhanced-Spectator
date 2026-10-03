namespace EnhancedSpectator.GameInterop
{
    // Consecutive frames expose frame-locked alternation; the cooldown is wall-clock based.
    internal sealed class NativeFadeDiagnosticSchedule
    {
        internal const int Frames = 12;
        private int _first = -100;
        private float _next;
        internal bool TryStart(int frame, float time)
        {
            if (time < _next) return false;
            _first = frame; _next = time + 8f; return true;
        }
        internal int Slot(int frame) => frame >= _first && frame - _first < Frames ? frame - _first : -1;
    }
}

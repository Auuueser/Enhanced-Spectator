namespace EnhancedSpectator.GameInterop
{
    // HDRP culls several cameras before executing their render requests. Restoring
    // a renderer for another camera must not cancel a previously culled draw job.
    internal sealed class NativeFadeRequestState
    {
        internal int Frame { get; private set; } = -1;
        private int _cameraId;
        internal void Begin(int frame, int cameraId) { Frame = frame; _cameraId = cameraId; }
        internal bool Matches(int frame, int cameraId) => Frame == frame && _cameraId == cameraId;
        internal bool EndCamera(int cameraId)
        {
            if (Frame < 0 || _cameraId != cameraId) return false;
            Clear(); return true;
        }
        internal void Clear() { Frame = -1; _cameraId = 0; }
    }
}

using UnityEngine;

namespace EnhancedSpectator.GameInterop
{
    // A monitor/UI camera may render the world, but cannot move a spectator's
    // presentation or change its layer for a pending player-view render request.
    internal static class FloatingHeadCameraOwnership
    {
        internal static bool CanUpdate(Camera renderingCamera, Camera activeView) =>
            renderingCamera != null && activeView != null && renderingCamera == activeView;
    }
}

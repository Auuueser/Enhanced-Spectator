using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Optional boundary for eye tracking, input focus, and camera collision queries.</summary>
public interface IGameSpectatorCameraAdapter
{
    /// <summary>Gets the current living target's synchronized eye pose.</summary>
    bool TryGetTargetEyePose(out Vector3 position, out Quaternion rotation);
    /// <summary>Whether a menu, chat, or focused text field owns keyboard/mouse input.</summary>
    bool IsCameraInputBlocked();
    /// <summary>Finds a safe camera distance against game world geometry.</summary>
    float GetSafeCameraDistance(Vector3 origin, Vector3 direction, float distance);
}

using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal interface IGameDirectorCameraAdapter : IGameMonitorCameraAdapter
{
    bool TryGetDirectorCompanion(Vector3 focus, out Vector3 companion);
    bool TryPlaceDirectorCamera(Vector3 focus, Vector3 candidate, out Vector3 camera);
}

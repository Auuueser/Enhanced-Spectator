using UnityEngine;

namespace EnhancedSpectator.GameInterop;

// A short probe of the already observed storey. This is a height suggestion only;
// missing geometry never prevents movement or selects a distant floor/ceiling.
internal interface IGameMonitorGroundReferenceAdapter
{
    bool TryGetMonitorGroundReference(Vector3 camera,float expectedHeight,out float height);
}

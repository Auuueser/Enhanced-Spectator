using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal interface IGameMonitorSupportAdapter
{
    bool TryGetMonitorSupport(out MonitorSupport support);
}

internal interface IGameMonitorEntranceAdapter
{
    bool TryGetMonitorElevatorApproach(out Bounds cabin);
    bool TryGetMonitorMainEntrance(out Vector3 point,out Vector3 axis);
}

internal readonly struct MonitorSupport
{
    internal readonly Transform Surface;
    internal readonly Vector3 Point;
    internal readonly Bounds Bounds;
    internal readonly bool KnownElevator;
    internal readonly float CeilingY;
    internal readonly Bounds? LocalBounds;
    internal bool OnRoof => float.IsNaN(CeilingY);
    internal MonitorSupport(Transform surface,Vector3 point,Bounds bounds,bool knownElevator=false,float ceilingY=float.NaN,Bounds? localBounds=null)
    { Surface=surface; Point=point; Bounds=bounds; KnownElevator=knownElevator; CeilingY=ceilingY; LocalBounds=localBounds; }
}

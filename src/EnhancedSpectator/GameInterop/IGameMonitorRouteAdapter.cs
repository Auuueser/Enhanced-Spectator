using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Read-only facility topology and camera-sized route probes.</summary>
internal interface IGameMonitorRouteAdapter : IGameMonitorCameraAdapter
{
    bool TryGetRouteRoom(Vector3 position, out MonitorRoom room);
    void GetMonitorPortals(int roomId, List<MonitorPortal> result);
    bool IsMonitorTravelClear(Vector3 from, Vector3 to);
    bool TryGetMonitorNavRoute(Vector3 from, Vector3 to, List<Vector3> points);
}

/// <summary>Read-only runtime evidence, requested only while the monitor diagnostic setting is enabled.</summary>
internal interface IGameMonitorDiagnosticsAdapter
{
    string DescribeMonitorGeometry(Vector3 cameraPosition);
}

/// <summary>Bounded, query-only local motion recovery. Does not move any game collider.</summary>
internal interface IGameMonitorMotionAdapter
{
    bool TryMoveMonitorCamera(Vector3 from,Vector3 requested,out Vector3 position,out bool sliding);
}

/// <summary>Optional travelling-only recovery: vertical walls are soft, floor/ceiling contacts stay solid.</summary>
internal interface IGameMonitorWallTransitAdapter
{
    bool IsMonitorWallTransitClear(Vector3 from,Vector3 to);
}

internal readonly struct MonitorPortal
{
    internal readonly int From, To;
    internal readonly Vector3 Entry, Center, Exit;
    internal MonitorPortal(int from,int to,Vector3 entry,Vector3 center,Vector3 exit)
    { From=from; To=to; Entry=entry; Center=center; Exit=exit; }
}

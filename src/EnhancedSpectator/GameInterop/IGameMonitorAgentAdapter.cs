using System.Collections.Generic;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

// These are navigation observations, never permission to constrain the camera transform.
internal enum MonitorAgentProbeStatus { Clear, Contact, Blocked, Deferred }

internal readonly struct MonitorAgentProbe
{
    internal readonly MonitorAgentProbeStatus Status;
    internal readonly float Distance;
    internal readonly Vector3 Normal;
    internal bool Passable => Status == MonitorAgentProbeStatus.Clear || Status == MonitorAgentProbeStatus.Contact;
    internal MonitorAgentProbe(MonitorAgentProbeStatus status, float distance = 0, Vector3 normal = default)
    { Status = status; Distance = distance; Normal = normal; }
}

internal interface IGameMonitorAgentAdapter
{
    void BeginMonitorAgentFrame(float now, int queryBudget);
    // Changes this frame's cumulative raw-query ceiling without resetting queries already spent.
    void SetMonitorAgentQueryLimit(int limit);
    int MonitorAgentQueries { get; }
    int MonitorAgentWorldRevision { get; }
    MonitorAgentProbe ProbeMonitorAgentTravel(Vector3 from, Vector3 to);
    MonitorAgentProbe ProbeMonitorAgentSight(Vector3 from, Vector3 to);
    bool TryGetMonitorAgentRoom(Vector3 position, out MonitorRoom room);
    bool TryGetMonitorAgentNavRoute(Vector3 from, Vector3 to, List<Vector3> result);
}

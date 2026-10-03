using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal interface IGameMonitorFramingAdapter
{
    MonitorFraming EvaluateMonitorFraming(Vector3 position, Quaternion rotation);
}

internal readonly struct MonitorFraming
{
    internal readonly bool Visible, Readable;
    internal MonitorFraming(bool visible, bool readable) { Visible=visible; Readable=readable; }
}

/// <summary>Confirmed facility geometry, separated from the spectator shot controller.</summary>
internal interface IGameMonitorCameraAdapter
{
    bool IsMonitorTargetIndoors { get; }
    bool TryGetMonitorRoom(out MonitorRoom room);
    bool TryPlaceMonitorCamera(Vector3 focus, Vector3 candidate, out Vector3 position);
    bool IsMonitorSightClear(Vector3 position, Vector3 focus);
    bool IsMonitorPathClear(Vector3 from, Vector3 to);
    void ClearMonitorRoom();
}

internal readonly struct MonitorRoom
{
    internal readonly int Id;
    internal readonly Bounds Bounds;
    internal readonly Matrix4x4 LocalToWorld, WorldToLocal;
    internal MonitorRoom(int id, Bounds bounds, Matrix4x4 localToWorld, Matrix4x4 worldToLocal)
    { Id = id; Bounds = bounds; LocalToWorld = localToWorld; WorldToLocal = worldToLocal; }
}

internal static class MonitorFrameGeometry
{
    internal static MonitorFraming Evaluate(Vector3 feet,Vector3 right,float height,Vector3 position,Quaternion rotation,float fov,float aspect,int mask)
    {
        int visible=0, inFrame=0;
        var inverse=Quaternion.Inverse(rotation);
        float tangent=Mathf.Tan(fov*Mathf.Deg2Rad*.5f);
        for(int i=0;i<5;i++)
        {
            Vector3 point=feet+Vector3.up*height*(i==0 ? .9f : i==1 ? .6f : i==2 ? .35f : .65f);
            if(i>=3) point+=right*(i==3 ? -1 : 1);
            if(!MonitorCameraGeometry.SightClear(position,point,mask)) continue;
            visible++;
            Vector3 local=inverse*(point-position);
            if(local.z>.01f && Mathf.Abs(local.y)<local.z*tangent*1.05f
                && Mathf.Abs(local.x)<local.z*tangent*aspect*1.05f) inFrame++;
        }
        float distance=(feet+Vector3.up*height*.5f-position).magnitude;
        return new MonitorFraming(visible>0,inFrame>0 && height/(2*tangent*Mathf.Max(.1f,distance))>=.03f);
    }
}

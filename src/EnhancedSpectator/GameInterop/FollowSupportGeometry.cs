using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal static class LethalCompanyFollowSupport
{
    internal static bool NearElevatorLanding(Bounds cabin,Vector3 landing,Vector3 feet,out Bounds approach)
    {
        // Landing references stay at their floor even when the cabin is at the opposite end.
        approach=new Bounds(landing+Vector3.up*(cabin.size.y*.5f),cabin.size);
        return NearElevator(approach,feet);
    }
    internal static bool NearElevator(Bounds cabin,Vector3 feet)
    {
        if(cabin.size.y<1 || cabin.size.y>8 || cabin.size.x>15 || cabin.size.z>15) return false;
        if(feet.y<cabin.min.y-1 || feet.y>cabin.max.y+.5f) return false;
        Vector3 nearest=cabin.ClosestPoint(feet),delta=nearest-feet; delta.y=0;
        return delta.sqrMagnitude<=64f;
    }

    internal static bool TryGetElevatorSupport(Transform frame,Bounds cabin,Vector3 feet,
        bool sampled,MonitorSupport sample,out MonitorSupport support)
    {
        support=default;
        if(!InElevatorOrOnRoof(cabin,feet)) return false;
        bool roof=feet.y>=cabin.max.y-.35f;
        // Cabin membership survives jumps and transient ray hits on a static landing/door sill.
        // On the roof retain the contact requirement so jumping away still releases the carrier.
        if(roof && (!sampled || Mathf.Abs(sample.Point.y-cabin.max.y)>.65f)) return false;
        float ceiling=roof ? float.NaN : cabin.max.y;
        if(!roof && sampled && !sample.OnRoof && sample.CeilingY>feet.y+.8f)
            ceiling=Mathf.Min(ceiling,sample.CeilingY);
        var point=new Vector3(feet.x,roof ? cabin.max.y : cabin.min.y,feet.z);
        support=new MonitorSupport(frame,point,cabin,true,ceiling);
        return true;
    }

    internal static bool InElevatorOrOnRoof(Bounds cabin,Vector3 feet)
    {
        // Reject shaft-sized/static trigger regions. The roof allowance is local to the cabin footprint.
        if(cabin.size.y>8 || cabin.size.y<1 || cabin.size.x>15 || cabin.size.z>15) return false;
        return feet.x>=cabin.min.x && feet.x<=cabin.max.x && feet.z>=cabin.min.z && feet.z<=cabin.max.z
            && feet.y>=cabin.min.y-.3f && feet.y<=cabin.max.y+1f;
    }
    internal static bool TryGetMonitorSupport(Vector3 feet,int mask,out MonitorSupport support)
    {
        support=default;
        // Only a nearby upward-facing surface is a carrier. Falling past a distant floor is not riding it.
        if(!Physics.Raycast(feet+Vector3.up*.35f,Vector3.down,out var hit,1f,mask,QueryTriggerInteraction.Ignore)
            || hit.normal.y<.65f) return false;
        // A nearby solid underside distinguishes a cabin from an open roof. Do not use the shaft's
        // distant ceiling or a door trigger as the cabin ceiling.
        float ceiling=float.NaN;
        if(Physics.Raycast(feet+Vector3.up*.4f,Vector3.up,out var overhead,4.1f,mask,QueryTriggerInteraction.Ignore)
            && overhead.normal.y<-.5f) ceiling=overhead.point.y;
        support=new MonitorSupport(hit.collider.transform,hit.point,hit.collider.bounds,false,ceiling);
        return true;
    }

    internal static bool TryGetGroundReference(Vector3 camera,float expectedHeight,int mask,out float height)
    {
        height=0;
        // Stay within the expected local storey. A long top-down ray can select
        // an overhead floor in a multi-storey building and send the camera upstairs.
        Vector3 origin=new Vector3(camera.x,expectedHeight+.65f,camera.z);
        if(!Physics.Raycast(origin,Vector3.down,out var hit,1.3f,mask,QueryTriggerInteraction.Ignore)
            || hit.normal.y<.65f || Mathf.Abs(hit.point.y-expectedHeight)>.65f) return false;
        height=hit.point.y; return true;
    }

    internal static bool TryGetHeight(Vector3 feet,int mask,out float height,out Transform? surface)
    {
        height=feet.y; surface=null;
        // Follow the actual floor/lift, not head bob or a short jump. A distant lower floor is not support.
        if (!Physics.Raycast(feet+Vector3.up*.35f,Vector3.down,out var hit,3f,mask,QueryTriggerInteraction.Ignore)
            || hit.normal.y<.5f) return false;
        height=hit.point.y; surface=hit.collider.transform;
        return true;
    }
}

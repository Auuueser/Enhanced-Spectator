using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal interface IGameFollowSupportAdapter
{
    bool TryGetFollowSupport(out float height, out Transform? surface);
}

public sealed partial class LethalCompanySpectatorAdapter : IGameFollowSupportAdapter, IGameMonitorSupportAdapter, IGameMonitorEntranceAdapter,
    IGameMonitorIndoorEntranceAdapter, IGameMonitorTraversalAdapter, IGameMonitorSubjectMotionAdapter, IGameMonitorGroundReferenceAdapter, IGameMonitorSubjectHeadingAdapter,
    IGameMonitorSubjectPostureAdapter
{
    private readonly MonitorElevatorGeometry _monitorElevatorGeometry=new MonitorElevatorGeometry();
    private EntranceTeleport[] _monitorEntrances=System.Array.Empty<EntranceTeleport>();
    private DunGen.Dungeon? _entranceDungeon;
    bool IGameMonitorEntranceAdapter.TryGetMonitorMainEntrance(out Vector3 point,out Vector3 axis)
        => TryGetIndoorEntrance(true,out point,out axis);
    bool IGameMonitorIndoorEntranceAdapter.TryGetMonitorIndoorEntrance(out Vector3 point,out Vector3 inward)
        => TryGetIndoorEntrance(false,out point,out inward);
    bool IGameMonitorTraversalAdapter.IsMonitorSubjectGrounded
    {
        get
        {
            var target=ViewTarget;
            if(target==null) return false;
            if(target.IsOwner && target.thisController!=null) return target.thisController.isGrounded;
            // A remote CharacterController is not moved locally. Sample the actual
            // feet instead of treating its stale isGrounded value as authoritative.
            return Physics.Raycast(target.transform.position+Vector3.up*.1f,Vector3.down,out var hit,.35f,
                target.walkableSurfacesNoPlayersMask,QueryTriggerInteraction.Ignore) && hit.normal.y>=.65f;
        }
    }
    bool IGameMonitorSubjectPostureAdapter.IsMonitorSubjectCrouching
        => ViewTarget is { } target && target.isCrouching;
    bool IGameMonitorSubjectMotionAdapter.TryGetMonitorSubjectFocus(out Vector3 focus)
    {
        focus=default;
        var target=ViewTarget;
        if(target==null || target.isPlayerDead || !target.isInsideFactory) return false;
        focus=target.transform.position+Vector3.up*1.05f;
        return true;
    }
    bool IGameMonitorSubjectHeadingAdapter.TryGetMonitorSubjectHeading(out Vector3 heading)
    {
        heading=default;
        var target=ViewTarget;
        if(target==null || target.isPlayerDead || !target.isInsideFactory) return false;
        heading=Vector3.ProjectOnPlane(target.transform.forward,Vector3.up);
        return heading.sqrMagnitude>.001f;
    }
    bool IGameMonitorGroundReferenceAdapter.TryGetMonitorGroundReference(Vector3 camera,float expectedHeight,out float height)
    {
        height=0;
        var target=ViewTarget;
        if(target==null || target.isPlayerDead || !target.isInsideFactory) return false;
        return LethalCompanyFollowSupport.TryGetGroundReference(camera,expectedHeight,target.walkableSurfacesNoPlayersMask,out height);
    }
    private bool TryGetIndoorEntrance(bool mainOnly,out Vector3 point,out Vector3 axis)
    {
        point=axis=default;
        var target=ViewTarget;
        var runtime=RoundManager.Instance!=null ? RoundManager.Instance.dungeonGenerator : null;
        var dungeon=runtime!=null ? runtime.Generator.CurrentDungeon : null;
        if(target==null || !target.isInsideFactory || target.isPlayerDead || dungeon==null) return false;
        if(_entranceDungeon!=dungeon)
        {
            _entranceDungeon=dungeon;
            _monitorEntrances=Object.FindObjectsByType<EntranceTeleport>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        }
        float best=64f;
        foreach(var entrance in _monitorEntrances)
        {
            if(entrance==null || mainOnly && entrance.entranceId!=0 || entrance.isEntranceToBuilding || !entrance.gameObject.activeInHierarchy) continue;
            var door=entrance.entrancePoint!=null ? entrance.entrancePoint : entrance.transform;
            Vector3 delta=target.transform.position-door.position;
            if(Mathf.Abs(delta.y)>4) continue;
            delta.y=0;
            if(delta.sqrMagnitude>=best) continue;
            best=delta.sqrMagnitude; point=door.position; axis=door.forward;
        }
        return best<64f;
    }
    bool IGameMonitorEntranceAdapter.TryGetMonitorElevatorApproach(out Bounds cabin)
    {
        cabin=default;
        var target=ViewTarget;
        var lift=RoundManager.Instance!=null ? RoundManager.Instance.currentMineshaftElevator : null;
        if(target==null || !target.isInsideFactory || target.isPlayerDead || lift==null || !lift.isActiveAndEnabled || lift.elevatorPoint==null
            || !_monitorElevatorGeometry.Resolve(lift.elevatorPoint,out cabin,out _)) return false;
        Vector3 feet=target.transform.position;
        Bounds movingCabin=cabin;
        if(lift.elevatorTopPoint!=null && LethalCompanyFollowSupport.NearElevatorLanding(
            movingCabin,lift.elevatorTopPoint.position,feet,out cabin)) return true;
        if(lift.elevatorBottomPointDoor!=null && LethalCompanyFollowSupport.NearElevatorLanding(
            movingCabin,lift.elevatorBottomPointDoor.position,feet,out cabin)) return true;
        cabin=movingCabin;
        return LethalCompanyFollowSupport.NearElevator(cabin,feet);
    }

    bool IGameMonitorSupportAdapter.TryGetMonitorSupport(out MonitorSupport support)
    {
        support=default;
        var target=ViewTarget;
        if(target==null || !target.isInsideFactory || target.isPlayerDead) return false;
        bool sampled=LethalCompanyFollowSupport.TryGetMonitorSupport(target.transform.position,target.walkableSurfacesNoPlayersMask,out support);
        // Confirmed V81 fields; read only. Motion detection remains available for other moving platforms.
        var lift=RoundManager.Instance!=null ? RoundManager.Instance.currentMineshaftElevator : null;
        if(lift!=null && lift.isActiveAndEnabled && lift.elevatorPoint!=null
            && _monitorElevatorGeometry.TrySupport(lift.elevatorPoint,target.transform.position,sampled,support,out var elevator))
        { support=elevator; return true; }
        return sampled;
    }

    bool IGameFollowSupportAdapter.TryGetFollowSupport(out float height, out Transform? surface)
    {
        height=0; surface=null;
        var target=ViewTarget;
        if(target==null) return false;
        return LethalCompanyFollowSupport.TryGetHeight(target.transform.position, target.walkableSurfacesNoPlayersMask, out height, out surface);
    }
}

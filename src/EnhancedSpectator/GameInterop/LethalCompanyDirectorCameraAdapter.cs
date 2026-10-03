using UnityEngine;
using GameNetcodeStuff;

namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter : IGameDirectorCameraAdapter
{
    private PlayerControllerB? _directorCompanion, _directorCompanionTarget;
    bool IGameDirectorCameraAdapter.TryGetDirectorCompanion(Vector3 focus, out Vector3 companion)
    {
        companion = default;
        var round = StartOfRound.Instance;
        var target = GetLocalPlayer()?.spectatedPlayerScript;
        if (round == null || target == null) return false;
        if (_directorCompanionTarget != target) { _directorCompanionTarget=target; _directorCompanion=null; }
        if (_directorCompanion != null && !_directorCompanion.isPlayerDead && _directorCompanion.isPlayerControlled
            && _directorCompanion.isInsideFactory == target.isInsideFactory)
        {
            Vector3 previous=_directorCompanion.transform.position+Vector3.up*1.2f;
            if ((previous-focus).sqrMagnitude<100f && MonitorCameraGeometry.SightClear(focus,previous,MonitorWorldMask))
            { companion=previous; return true; }
        }
        _directorCompanion=null;
        float best = 64f; bool found = false;
        foreach (var player in round.allPlayerScripts)
        {
            if (player == null || player == target || player.isPlayerDead || !player.isPlayerControlled
                || player.isInsideFactory != target.isInsideFactory) continue;
            Vector3 point = player.transform.position + Vector3.up * 1.2f;
            float distance = (point - focus).sqrMagnitude;
            if (distance >= best || !MonitorCameraGeometry.SightClear(focus, point, MonitorWorldMask)) continue;
            best = distance; companion = point; found = true; _directorCompanion=player;
        }
        return found;
    }

    bool IGameDirectorCameraAdapter.TryPlaceDirectorCamera(Vector3 focus, Vector3 candidate, out Vector3 camera)
    {
        camera = candidate;
        Vector3 delta = candidate - focus; float length = Mathf.Min(delta.magnitude, 12f);
        if (length < 1.8f || Physics.CheckSphere(focus,.12f,MonitorWorldMask,QueryTriggerInteraction.Ignore)) return false;
        if (Physics.SphereCast(focus,.3f,delta.normalized,out var hit,length,MonitorWorldMask,QueryTriggerInteraction.Ignore))
            length = Mathf.Max(0,hit.distance-.15f);
        camera = focus + delta.normalized*length;
        return length >= 1.8f && MonitorCameraGeometry.PathClear(camera,camera,MonitorWorldMask)
            && MonitorCameraGeometry.SightClear(camera,focus,MonitorWorldMask);
    }
}

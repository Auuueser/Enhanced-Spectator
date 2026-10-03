using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Actual indoor arrival doorway; separate from main-entrance shot composition.</summary>
internal interface IGameMonitorIndoorEntranceAdapter
{
    bool TryGetMonitorIndoorEntrance(out Vector3 point,out Vector3 inward);
}

/// <summary>Confirmed character support state, independent of camera collision references.</summary>
internal interface IGameMonitorTraversalAdapter
{
    bool IsMonitorSubjectGrounded { get; }
}

internal interface IGameMonitorSubjectMotionAdapter
{
    bool TryGetMonitorSubjectFocus(out Vector3 focus);
}

/// <summary>Subject posture; a crouching subject is framed closer.</summary>
internal interface IGameMonitorSubjectPostureAdapter
{
    bool IsMonitorSubjectCrouching { get; }
}

/// <summary>Initial subject heading; look changes do not continually turn a travelling rig.</summary>
internal interface IGameMonitorSubjectHeadingAdapter
{
    bool TryGetMonitorSubjectHeading(out Vector3 heading);
}

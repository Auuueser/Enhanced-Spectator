using UnityEngine;

namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Reconstructs a sampled world pose in the current frame of a moving reference.
/// </summary>
public static class RemoteSpectatorMotionCompensationRules
{
    /// <summary>Captures a world point in the moving reference's local space.</summary>
    public static Vector3 CaptureLocalPosition(
        Vector3 worldPosition,
        SpectatorMotionReferencePose reference)
    {
        return Rotate(Inverse(reference.Rotation), worldPosition - reference.Position);
    }

    /// <summary>Captures a world rotation in the moving reference's local space.</summary>
    public static Quaternion CaptureLocalRotation(
        Quaternion worldRotation,
        SpectatorMotionReferencePose reference)
    {
        return Multiply(Inverse(reference.Rotation), worldRotation);
    }

    /// <summary>Reconstructs a reference-local point in the current world frame.</summary>
    public static Vector3 ResolveLocalPosition(
        Vector3 localPosition,
        SpectatorMotionReferencePose currentReference)
    {
        return currentReference.Position + Rotate(currentReference.Rotation, localPosition);
    }

    /// <summary>Reconstructs a reference-local rotation in the current world frame.</summary>
    public static Quaternion ResolveLocalRotation(
        Quaternion localRotation,
        SpectatorMotionReferencePose currentReference)
    {
        return Multiply(currentReference.Rotation, localRotation);
    }

    /// <summary>Moves a sampled point with the reference motion observed after the sample.</summary>
    public static Vector3 ResolvePosition(
        Vector3 sampledWorldPosition,
        SpectatorMotionReferencePose sampledReference,
        SpectatorMotionReferencePose currentReference)
    {
        Vector3 localPosition = CaptureLocalPosition(sampledWorldPosition, sampledReference);
        return ResolveLocalPosition(localPosition, currentReference);
    }

    /// <summary>Moves a sampled rotation with the reference motion observed after the sample.</summary>
    public static Quaternion ResolveRotation(
        Quaternion sampledWorldRotation,
        SpectatorMotionReferencePose sampledReference,
        SpectatorMotionReferencePose currentReference)
    {
        Quaternion localRotation = CaptureLocalRotation(sampledWorldRotation, sampledReference);
        return ResolveLocalRotation(localRotation, currentReference);
    }

    private static Quaternion Inverse(Quaternion value)
    {
        float norm = (value.x * value.x) + (value.y * value.y) + (value.z * value.z) + (value.w * value.w);
        if (norm <= 0.000001f)
        {
            return Quaternion.identity;
        }

        float reciprocal = 1f / norm;
        return new Quaternion(-value.x * reciprocal, -value.y * reciprocal, -value.z * reciprocal, value.w * reciprocal);
    }

    private static Quaternion Multiply(Quaternion left, Quaternion right)
    {
        return new Quaternion(
            (left.w * right.x) + (left.x * right.w) + (left.y * right.z) - (left.z * right.y),
            (left.w * right.y) - (left.x * right.z) + (left.y * right.w) + (left.z * right.x),
            (left.w * right.z) + (left.x * right.y) - (left.y * right.x) + (left.z * right.w),
            (left.w * right.w) - (left.x * right.x) - (left.y * right.y) - (left.z * right.z));
    }

    private static Vector3 Rotate(Quaternion rotation, Vector3 point)
    {
        float x2 = rotation.x + rotation.x;
        float y2 = rotation.y + rotation.y;
        float z2 = rotation.z + rotation.z;
        float xx2 = rotation.x * x2;
        float yy2 = rotation.y * y2;
        float zz2 = rotation.z * z2;
        float xy2 = rotation.x * y2;
        float xz2 = rotation.x * z2;
        float yz2 = rotation.y * z2;
        float wx2 = rotation.w * x2;
        float wy2 = rotation.w * y2;
        float wz2 = rotation.w * z2;
        return new Vector3(
            ((1f - yy2) - zz2) * point.x + (xy2 - wz2) * point.y + (xz2 + wy2) * point.z,
            (xy2 + wz2) * point.x + ((1f - xx2) - zz2) * point.y + (yz2 - wx2) * point.z,
            (xz2 - wy2) * point.x + (yz2 + wx2) * point.y + ((1f - xx2) - yy2) * point.z);
    }
}

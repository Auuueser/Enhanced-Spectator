namespace EnhancedSpectator.Features.SpectatorPresence;

/// <summary>
/// Pure rules for retaining the moving ship frame while the takeoff animation is active.
/// </summary>
public static class ShipMotionReferenceCaptureRules
{
    /// <summary>Gets whether a spectator pose should be captured relative to the ship.</summary>
    public static bool ShouldCapture(
        bool isInsideShipBounds,
        bool shipIsLeaving,
        bool wasMotionReferenced)
    {
        return isInsideShipBounds || (shipIsLeaving && wasMotionReferenced);
    }
}

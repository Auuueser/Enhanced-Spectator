namespace EnhancedSpectator.Features.Spectator;

/// <summary>Death-only handoffs keep the existing automatic target choice and the 50 metre boundary.</summary>
public static class SpectatorDeathHandoffRules
{
    /// <summary>Allows a spatial handoff only for an automatic death replacement at most 50 metres away.</summary>
    public static bool ShouldTravel(bool automatic, bool previousTargetDead, bool previousTargetIndoors,
        bool nextTargetIndoors, bool nextTargetValid, bool targetChanged, float subjectDistanceSquared)
        => automatic && previousTargetDead && previousTargetIndoors && nextTargetIndoors
            && nextTargetValid && targetChanged && subjectDistanceSquared <= 50f * 50f;
}

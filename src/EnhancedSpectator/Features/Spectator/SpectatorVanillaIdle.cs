namespace EnhancedSpectator.Features.Spectator;

/// <summary>Idle mouse time belonging to the current vanilla spectator view.</summary>
internal sealed class SpectatorVanillaIdle
{
    private float _seconds;
    internal bool IsIdle => _seconds >= SpectatorAutoCenter.IdleSeconds;

    internal void Update(bool vanillaView, bool mouseActivity, float deltaTime)
        => _seconds = !vanillaView || mouseActivity ? 0 : _seconds + deltaTime;
}

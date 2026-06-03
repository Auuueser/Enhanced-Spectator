namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Adapter boundary for invoking vanilla spectator target switching after disconnect cleanup.
/// </summary>
public interface IGameSpectatorTargetSwitchAdapter
{
    /// <summary>
    /// Attempts to read the local disconnect target-switch context.
    /// </summary>
    bool TryGetDisconnectTargetSwitchContext(out SpectatorDisconnectTargetSwitchContext context);

    /// <summary>
    /// Attempts to invoke vanilla spectator target switching once.
    /// </summary>
    bool TrySwitchToNextSpectatorTarget();
}

internal sealed class NoopGameSpectatorTargetSwitchAdapter : IGameSpectatorTargetSwitchAdapter
{
    public static NoopGameSpectatorTargetSwitchAdapter Instance { get; } = new NoopGameSpectatorTargetSwitchAdapter();

    private NoopGameSpectatorTargetSwitchAdapter()
    {
    }

    public bool TryGetDisconnectTargetSwitchContext(out SpectatorDisconnectTargetSwitchContext context)
    {
        context = default;
        return false;
    }

    public bool TrySwitchToNextSpectatorTarget()
    {
        return false;
    }
}

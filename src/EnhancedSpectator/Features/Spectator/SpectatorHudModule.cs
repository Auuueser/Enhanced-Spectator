using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Runtime;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Updates optional clock/name compatibility independently of networking.</summary>
public sealed class SpectatorHudModule : IFeatureModule, IRuntimeLateTickable
{
    private readonly IGameSpectatorHudAdapter _adapter;
    /// <summary>Creates a local HUD compatibility module.</summary>
    public SpectatorHudModule(IGameSpectatorHudAdapter adapter) => _adapter = adapter;
    /// <inheritdoc />
    public void Initialize() { }
    /// <inheritdoc />
    public void LateTick()
    {
        if (RuntimeConnectionState.CanRunLocalDiagnostics(out _)) _adapter.UpdateLayout();
        else _adapter.Dispose();
    }
    /// <inheritdoc />
    public void Dispose() => _adapter.Dispose();
}

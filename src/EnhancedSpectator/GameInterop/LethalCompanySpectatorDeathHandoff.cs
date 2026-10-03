namespace EnhancedSpectator.GameInterop;

public sealed partial class LethalCompanySpectatorAdapter : IGameSpectatorDeathHandoffAdapter
{
    /// <inheritdoc />
    public bool TryGetSpectatedHandoffTarget(out SpectatorHandoffTarget target)
    {
        var player = GetLocalPlayer()?.spectatedPlayerScript;
        if (player == null)
        {
            target = default;
            return false;
        }

        target = new SpectatorHandoffTarget(player.playerClientId, player.actualClientId,
            player.transform.position, player.isPlayerDead, player.isInsideFactory,
            IsValidSpectateTarget(player));
        return true;
    }
}

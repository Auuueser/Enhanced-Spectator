using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SpectatorPresence;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>One per-frame owner budget for fear models, default heads, local avatars and their labels.</summary>
internal static class LethalCompanyModelVisibility
{
    private static EnhancedSpectatorConfig? _config;
    private static ISpectatorPresenceProvider? _presence;
    private static SpectatorModule? _spectator;
    private static RemoteSpectatorPosePresentationService? _poses;
    private static readonly SpectatorModelBudget Budget = new SpectatorModelBudget();
    private static int _frame = -1, _limit = -1;
    internal static void Configure(EnhancedSpectatorConfig config, ISpectatorPresenceProvider presence,
        SpectatorModule spectator, RemoteSpectatorPosePresentationService poses)
    { Clear(); _config=config; _presence=presence; _spectator=spectator; _poses=poses; }
    internal static void Clear() { _config=null; _presence=null; _spectator=null; _poses=null; _frame=-1; _limit=-1; Budget.Clear(); }
    internal static bool ShouldHideAutoCentering(RemoteSpectatorInfo spectator)
    {
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        return SpectatorAutoCenter.ShouldHideRemoteModel(
            _config?.Camera.HideAutoCenteringModels.Value == true,
            local != null && !local.isPlayerDead, spectator.PoseState?.AutoCentering == true);
    }
    internal static bool Allows(ulong owner)
    {
        if (_config == null) return true;
        if (_config.Camera.HideAllModels.Value) return false;
        int limit = _config.Camera.ModelDisplayLimit.Value;
        if (limit == 0) return true;
        if (_frame != Time.frameCount || _limit != limit)
        {
            _frame=Time.frameCount; _limit=limit;
            var round=StartOfRound.Instance;
            var local=round!=null ? round.localPlayerController : null;
            Vector3 viewer=round!=null && round.activeCamera!=null ? round.activeCamera.transform.position
                : local!=null ? local.transform.position : Vector3.zero;
            Budget.Begin();
            if (_presence != null)
                foreach(var info in _presence.Current.RemoteSpectators)
                {
                    if(info.PoseState?.ModelStowed==true || ShouldHideAutoCentering(info)) continue;
                    Vector3 position=viewer;
                    if(info.PoseState!=null && _poses!=null) _poses.Resolve(info.PoseState,out position,out _,out _);
                    Budget.Add(info.SpectatorClientId,(position-viewer).sqrMagnitude);
                }
            if(local!=null && local.isPlayerDead && _spectator?.CameraState is { IsThirdPerson:true, HasWorldPose:true } state)
                Budget.Add(local.actualClientId,(state.WorldPosition-viewer).sqrMagnitude);
            Budget.Finish(limit);
        }
        return Budget.Contains(owner);
    }
}

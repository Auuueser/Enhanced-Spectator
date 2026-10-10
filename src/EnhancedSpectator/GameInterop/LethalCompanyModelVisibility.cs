using System.Collections.Generic;
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
    private static readonly Dictionary<ulong, SpectatorCenteringReveal> Reveals = new();
    private static int _frame = -1, _limit = -1;
    internal static void Configure(EnhancedSpectatorConfig config, ISpectatorPresenceProvider presence,
        SpectatorModule spectator, RemoteSpectatorPosePresentationService poses)
    { Clear(); _config=config; _presence=presence; _spectator=spectator; _poses=poses; }
    internal static void Clear() { _config=null; _presence=null; _spectator=null; _poses=null; _frame=-1; _limit=-1; Budget.Clear(); Reveals.Clear(); }
    internal static bool ShouldHideAutoCentering(RemoteSpectatorInfo spectator)
        => InFormation(spectator) || SpectatorAutoCenter.ShouldHideRemoteModel(
            _config?.Camera.HideAutoCenteringModels.Value == true, spectator.PoseState?.AutoCentering == true);

    /// <summary>
    /// A watch-together follower standing in its leader's formation: hidden however its leader's camera moves, and
    /// shown only while it speaks, translucent at its place, like a speaking spectator revealed while centering
    /// (whatever that option says). Leaving the formation, it continues from that opacity under the usual rules.
    /// </summary>
    internal static bool InFormation(RemoteSpectatorInfo spectator) => _poses?.Parties?.FormationLeader(spectator.SpectatorClientId) != null;
    /// <summary>
    /// Advances a spectator's idle-centering reveal once per frame (default-head service) and returns its
    /// opacity ceiling: 0 hidden, translucent while a hidden spectator speaks, 1 otherwise.
    /// </summary>
    internal static float UpdateCenteringOpacity(RemoteSpectatorInfo spectator, bool speaking, float deltaTime)
    {
        bool hidden = ShouldHideAutoCentering(spectator);
        if (!Reveals.TryGetValue(spectator.SpectatorClientId, out var reveal))
        {
            if (!hidden) return 1;
            Reveals.Add(spectator.SpectatorClientId, reveal = new SpectatorCenteringReveal(true));
        }
        return reveal.Update(hidden, _config?.Camera.RevealSpeakingCenteringModels.Value == true || InFormation(spectator), speaking, deltaTime);
    }
    /// <summary>Current ceiling for other model kinds; without a reveal state the plain centering rule applies.</summary>
    internal static float CenteringOpacity(RemoteSpectatorInfo spectator)
        => Reveals.TryGetValue(spectator.SpectatorClientId, out var reveal) ? reveal.Opacity : ShouldHideAutoCentering(spectator) ? 0 : 1;
    internal static void ForgetCentering(ulong spectator) => Reveals.Remove(spectator);
    /// <summary>
    /// The spectator's model is put away: by their camera (first person and the like), by sitting in the split-screen
    /// audience, because this viewer is in split-screen and they are not, or because they watch together with this
    /// viewer (through the same camera).
    /// </summary>
    internal static bool Stowed(RemoteSpectatorInfo spectator) => spectator.PoseState is { } pose
        && (pose.ModelStowed || Features.SpectatorPresence.SplitScreenPresenceRules.HidesModel(pose.SplitView, Features.SplitScreen.SplitScreenModule.LocalView)
            || _poses?.Parties?.InLocalParty(spectator.SpectatorClientId) == true);
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
                    if(Stowed(info) || CenteringOpacity(info)<=0) continue;
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

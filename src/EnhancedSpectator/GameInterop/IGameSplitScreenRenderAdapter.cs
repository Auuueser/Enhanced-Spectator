using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SplitScreen;
using EnhancedSpectator.Features.SpectatorPresence;
using EnhancedSpectator.Networking;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

internal interface IGameSplitScreenRenderAdapter : IDisposable
{
    bool TryBegin(bool preview);
    void BindPrimary(SplitScreenParticipant? participant, bool focused);
    void SetPrimaryMode(SpectatorCameraMode? mode, int monitorStyle);
    SpectatorCameraMode? PrimaryMode { get; }
    float PrimaryTransitionOpacity { get; }
    void UpdatePrimaryPose();
    /// <summary>
    /// After <see cref="UpdatePrimaryPose"/>, watching together: the large view is posed as the followed spectator's
    /// completed camera timeline. Following owns input even while a matching sample is missing.
    /// An audience member has no large-view pose to mirror; the local camera continues to follow its player.
    /// Ending following returns control and the local lens.
    /// </summary>
    void MirrorPrimaryPose(SpectatorCameraSample? pose, SpectatorSplitView followedView);
    /// <summary>The participant's player is inside the facility (where the choreography camera works).</summary>
    bool IsIndoors(SplitScreenParticipant participant);
    /// <summary>Preview only: the self third-person ghost floats in front of the large view's player, facing them.</summary>
    void FacePreviewGhost();
    void InvalidateRooms();
    void SetTileShadowCascades(int cascades);
    void RefreshVisibleRooms(IReadOnlyList<SplitScreenParticipant> participants);
    Texture? RenderView(SplitScreenParticipant participant, bool primary, int width, int height);
    /// <summary>All crew dead: the game's own game-over camera (the ship taking off), as the game poses it.</summary>
    Texture? RenderGameView(SplitScreenKey key, int width, int height);
    Texture? GetTexture(SplitScreenKey key);
    void BeginViewTransition(SplitScreenKey key);
    void ReleaseView(SplitScreenKey key);
    void CollectRetiredTextures(Predicate<Texture> inUse);
    int CameraCount { get; }
    long TextureBytes { get; }
    /// <summary>Gives the cameras back; textures <paramref name="keep"/> still shows stay until collected.</summary>
    void Restore(Predicate<Texture>? keep = null);
}

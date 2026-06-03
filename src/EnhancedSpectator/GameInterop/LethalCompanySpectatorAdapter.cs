using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// Reads confirmed Lethal Company spectator state through direct game member access.
/// </summary>
public sealed class LethalCompanySpectatorAdapter : IGameSpectatorAdapter
{
    /// <inheritdoc />
    public SpectatorState ReadSpectatorState()
    {
        if (!TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot))
        {
            return SpectatorState.Unavailable;
        }

        string status = snapshot.IsLocalPlayerDead
            ? "Local player is in spectator-compatible dead state."
            : "Local player is not dead.";

        return new SpectatorState(
            true,
            status,
            snapshot.IsLocalPlayerDead,
            false);
    }

    /// <inheritdoc />
    public bool TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null)
        {
            snapshot = GameSpectatorSnapshot.Unavailable;
            return false;
        }

        PlayerControllerB localPlayer = round.localPlayerController;
        if (localPlayer == null)
        {
            bool isSpectateCameraActive = IsSpectateCameraActive(round);
            snapshot = new GameSpectatorSnapshot(
                true,
                false,
                false,
                false,
                false,
                round.overrideSpectateCamera,
                isSpectateCameraActive,
                round.spectateCamera,
                null,
                null,
                null,
                null,
                null);
            return true;
        }

        PlayerControllerB spectatedPlayer = localPlayer.spectatedPlayerScript;
        bool hasValidTarget = IsValidSpectateTarget(spectatedPlayer);
        Transform? anchor = hasValidTarget ? ResolveAnchor(spectatedPlayer) : null;
        ulong? slotId = hasValidTarget ? spectatedPlayer.playerClientId : null;
        ulong? actualClientId = hasValidTarget ? spectatedPlayer.actualClientId : null;
        bool isActiveCamera = IsSpectateCameraActive(round);

        snapshot = new GameSpectatorSnapshot(
            true,
            true,
            localPlayer.isPlayerDead,
            localPlayer.hasBegunSpectating,
            hasValidTarget,
            round.overrideSpectateCamera,
            isActiveCamera,
            round.spectateCamera,
            anchor,
            localPlayer.playerClientId,
            localPlayer.actualClientId,
            slotId,
            actualClientId);

        return true;
    }

    /// <inheritdoc />
    public bool IsLocalPlayerSpectating()
    {
        return TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.HasRound
            && snapshot.HasLocalPlayer
            && snapshot.IsLocalPlayerDead
            && snapshot.SpectateCamera != null;
    }

    /// <inheritdoc />
    public bool TryGetSpectatedPlayerAnchor(out Transform? anchor)
    {
        if (TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot) && snapshot.Anchor != null)
        {
            anchor = snapshot.Anchor;
            return true;
        }

        anchor = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSpectateCamera(out Camera? camera)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round != null && round.spectateCamera != null)
        {
            camera = round.spectateCamera;
            return true;
        }

        camera = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetActiveCamera(out Camera? camera)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round != null && round.activeCamera != null)
        {
            camera = round.activeCamera;
            return true;
        }

        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer != null && localPlayer.gameplayCamera != null)
        {
            camera = localPlayer.gameplayCamera;
            return true;
        }

        camera = null;
        return false;
    }

    /// <inheritdoc />
    public bool IsGameOverSpectateOverrideActive()
    {
        StartOfRound round = StartOfRound.Instance;
        return round != null && round.overrideSpectateCamera;
    }

    /// <inheritdoc />
    public bool IsLocalQuickMenuOpen()
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == null || localPlayer.quickMenuManager == null)
        {
            return false;
        }

        return localPlayer.quickMenuManager.isMenuOpen;
    }

    /// <inheritdoc />
    public bool IsValidSpectateTarget(object? target)
    {
        PlayerControllerB? player = target as PlayerControllerB;
        StartOfRound round = StartOfRound.Instance;
        return LethalCompanySpectatorTargetRules.IsValidSpectateTarget(round, player);
    }

    /// <inheritdoc />
    public bool TryGetSpectatedPlayerId(out ulong slotId, out ulong actualClientId)
    {
        if (TryGetLocalSpectatorSnapshot(out GameSpectatorSnapshot snapshot)
            && snapshot.SpectatedPlayerSlotId.HasValue
            && snapshot.SpectatedPlayerActualClientId.HasValue)
        {
            slotId = snapshot.SpectatedPlayerSlotId.Value;
            actualClientId = snapshot.SpectatedPlayerActualClientId.Value;
            return true;
        }

        slotId = 0;
        actualClientId = 0;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetLocalPlayerIdentity(out ulong clientId, out ulong slotId)
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            clientId = localPlayer.actualClientId;
            slotId = localPlayer.playerClientId;
            return true;
        }

        clientId = 0;
        slotId = 0;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetPlayerDisplayName(ulong clientId, ulong slotId, out string displayName)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null)
        {
            displayName = string.Empty;
            return false;
        }

        bool foundActualClientEntry = false;
        for (int index = 0; index < round.allPlayerScripts.Length; index++)
        {
            PlayerControllerB player = round.allPlayerScripts[index];
            if (player == null || player.actualClientId != clientId)
            {
                continue;
            }

            foundActualClientEntry = true;
            if (PlayerDisplayNameRules.TryNormalize(player.playerUsername, out displayName))
            {
                return true;
            }
        }

        if (foundActualClientEntry)
        {
            displayName = string.Empty;
            return false;
        }

        for (int index = 0; index < round.allPlayerScripts.Length; index++)
        {
            PlayerControllerB player = round.allPlayerScripts[index];
            if (player == null)
            {
                continue;
            }

            if (player.playerClientId == slotId
                && PlayerDisplayNameRules.TryNormalize(player.playerUsername, out displayName))
            {
                return true;
            }
        }

        displayName = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetLocalVoicePlayerName(out string voicePlayerName)
    {
        StartOfRound round = StartOfRound.Instance;
        if (round != null
            && round.voiceChatModule != null
            && !string.IsNullOrWhiteSpace(round.voiceChatModule.LocalPlayerName))
        {
            voicePlayerName = round.voiceChatModule.LocalPlayerName.Trim();
            return true;
        }

        voicePlayerName = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetLocalPlayerHeadPoint(out Transform? anchor)
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            anchor = null;
            return false;
        }

        if (TryFindHeadPoint(localPlayer.playerGlobalHead, out anchor))
        {
            return true;
        }

        anchor = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetLocalPlayerHeadAnchor(out Transform? anchor)
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            anchor = null;
            return false;
        }

        if (TryGetLocalPlayerHeadPoint(out anchor))
        {
            return true;
        }

        if (localPlayer.playerGlobalHead != null)
        {
            anchor = localPlayer.playerGlobalHead;
            return true;
        }

        if (localPlayer.headCostumeContainer != null)
        {
            anchor = localPlayer.headCostumeContainer;
            return true;
        }

        anchor = localPlayer.transform;
        return anchor != null;
    }

    /// <inheritdoc />
    public bool TryGetLocalPlayerHeadAnchorPosition(out Vector3 position)
    {
        PlayerControllerB? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            position = Vector3.zero;
            return false;
        }

        if (TryGetLocalPlayerHeadAnchor(out Transform? anchor) && anchor != null)
        {
            position = anchor == localPlayer.transform
                ? anchor.position + Vector3.up
                : anchor.position;
            return true;
        }

        position = localPlayer.transform != null
            ? localPlayer.transform.position + Vector3.up
            : Vector3.zero;
        return localPlayer.transform != null;
    }

    private static Transform? ResolveAnchor(PlayerControllerB player)
    {
        Transform? root = player.transform;
        SpectatorTargetAnchorSource source = SpectatorTargetAnchorSelectionRules.Resolve(
            root != null,
            player.lowerSpine != null,
            player.playerGlobalHead != null);

        if (source == SpectatorTargetAnchorSource.PlayerRoot)
        {
            return root;
        }

        if (source == SpectatorTargetAnchorSource.LowerSpine)
        {
            return player.lowerSpine;
        }

        if (source == SpectatorTargetAnchorSource.GlobalHead)
        {
            return player.playerGlobalHead;
        }

        return null;
    }

    private static PlayerControllerB? GetLocalPlayer()
    {
        StartOfRound round = StartOfRound.Instance;
        return round != null ? round.localPlayerController : null;
    }

    private static bool TryFindHeadPoint(Transform? root, out Transform? headPoint)
    {
        if (root == null)
        {
            headPoint = null;
            return false;
        }

        if (root.name == "HeadPoint")
        {
            headPoint = root;
            return true;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (TryFindHeadPoint(child, out headPoint))
            {
                return true;
            }
        }

        headPoint = null;
        return false;
    }

    private static bool IsSpectateCameraActive(StartOfRound round)
    {
        return round.spectateCamera != null && round.activeCamera == round.spectateCamera;
    }
}

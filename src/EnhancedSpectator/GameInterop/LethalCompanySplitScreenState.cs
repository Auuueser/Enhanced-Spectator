using System.Collections.Generic;
using EnhancedSpectator.Features.SplitScreen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

internal readonly struct SplitScreenSessionContext
{
    internal readonly int Session;
    // GameUiOpen: the quick menu or a text field (chat) is open; window focus loss alone does not count.
    // AllDead: the whole crew is dead and the ship leaves (or has left) without them.
    // GameOverCamera: the game watches the ship take off from outside (all dead, or leaving early by vote or at
    // midnight, with living crew aboard).
    internal readonly bool Dead, Eligible, MenuOpen, GameUiOpen, InputBlocked, PreviewInputBlocked, AllDead, ShipLeaving, GameOverCamera;
    // Chat: the game's chat line is being typed (the split-screen shows it in its own panel, over the views).
    internal readonly bool Chat;
    internal readonly SplitScreenKey? Target;
    internal SplitScreenSessionContext(int session, bool dead, bool eligible, bool menuOpen, bool gameUiOpen, bool inputBlocked, bool previewInputBlocked,
        SplitScreenKey? target, bool allDead = false, bool shipLeaving = false, bool chat = false, bool gameOverCamera = false)
    {
        Session = session; Dead = dead; Eligible = eligible; MenuOpen = menuOpen; GameUiOpen = gameUiOpen;
        InputBlocked = inputBlocked; PreviewInputBlocked = previewInputBlocked; Target = target; AllDead = allDead; ShipLeaving = shipLeaving; Chat = chat;
        GameOverCamera = gameOverCamera;
    }
}

internal sealed class LethalCompanySplitScreenState
{
    private readonly LethalCompanySpectatorAdapter _input = new();
    private readonly LethalCompanyVoiceActivityAdapter _voice = new();
    private RawImage? _hiddenScreen;
    private bool _screenWasEnabled;

    /// <summary>
    /// The preview's pointer key. Alive, the pointer is the game's quick menu and camera control is the character,
    /// so the key opens or closes that menu, as the pointer key switches pointer and camera while spectating.
    /// </summary>
    internal void TogglePreviewPointer()
    {
        var menu = StartOfRound.Instance?.localPlayerController?.quickMenuManager;
        if (menu == null) return;
        if (menu.isMenuOpen) menu.CloseQuickMenu(); else menu.OpenQuickMenu();
    }

    internal SplitScreenSessionContext Read()
    {
        var round = StartOfRound.Instance;
        var local = round != null ? round.localPlayerController : null;
        if (local == null) return default;
        var target = local.spectatedPlayerScript;
        SplitScreenKey? key = target != null && !target.isPlayerDead && !target.disconnectedMidGame
            ? new SplitScreenKey(target.actualClientId, target.playerClientId) : null;
        bool menu = local.quickMenuManager != null && local.quickMenuManager.isMenuOpen;
        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        bool textEntry = local.isTypingChat || (selected != null && (selected.GetComponent<TMP_InputField>() != null
            || selected.GetComponent<UnityEngine.UI.InputField>() != null));
        // The game-over camera (all crew dead, or the ship leaving early by vote or at midnight) stays eligible: the
        // split-screen shows the ship taking off through it.
        return new SplitScreenSessionContext(round!.GetInstanceID(), local.isPlayerDead,
            local.isPlayerDead && local.isInGameOverAnimation <= 0 && round.spectateCamera != null && round.activeCamera == round.spectateCamera,
            menu, menu || textEntry, !Application.isFocused || _input.IsUiInputBlocked(), !Application.isFocused || textEntry, key,
            round.allPlayersDead, round.shipIsLeaving, local.isTypingChat, round.overrideSpectateCamera);
    }

    /// <summary>
    /// While the quick menu or chat is open the split-screen draws beneath the game's HUD canvas, so the menu,
    /// its added pages and the chat stay readable over live views. The vanilla screen image on that canvas
    /// still holds the spectate camera's frame from before the split-screen borrowed it, so it is hidden
    /// meanwhile. Returns the canvas to draw beneath, or null once restored.
    /// </summary>
    /// <summary>The game HUD's root canvas (its menus and chat draw on it).</summary>
    internal static Canvas? HudCanvas => HUDManager.Instance != null && HUDManager.Instance.playerScreenTexture != null
        ? HUDManager.Instance.playerScreenTexture.canvas.rootCanvas : null;

    /// <summary>The game's live view: its camera draws into the HUD's player screen.</summary>
    internal static Texture? LiveView => HUDManager.Instance != null && HUDManager.Instance.playerScreenTexture != null
        ? HUDManager.Instance.playerScreenTexture.texture : null;

    internal Canvas? HudCanvasBeneath(bool beneath)
    {
        var screen = beneath && HUDManager.Instance != null ? HUDManager.Instance.playerScreenTexture : null;
        if (screen != _hiddenScreen)
        {
            if (_hiddenScreen != null) _hiddenScreen.enabled = _screenWasEnabled;
            _hiddenScreen = screen;
            if (screen != null) { _screenWasEnabled = screen.enabled; screen.enabled = false; }
        }
        LethalCompanySplitScreenUiPresentation.Surface = screen;
        return screen != null ? screen.canvas.rootCanvas : null;
    }

    internal void CopyLivingPlayersTo(List<SplitScreenParticipant> output, bool includeLocal)
    {
        output.Clear();
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null || round.allPlayerScripts == null) return;
        foreach (var entry in round.ClientPlayerList)
        {
            int slot = entry.Value;
            if (slot < 0 || slot >= round.allPlayerScripts.Length) continue;
            var player = round.allPlayerScripts[slot];
            if (player == null || player.actualClientId != entry.Key || player.disconnectedMidGame
                || player.isPlayerDead || !player.isPlayerControlled || (!includeLocal && player == round.localPlayerController)) continue;
            var key = new SplitScreenKey(entry.Key, (ulong)slot);
            output.Add(new SplitScreenParticipant(key, key, PlayerDisplayNames.Of(player)));
        }
        output.Sort(BySlot.Instance);
        if (output.Count > 31) output.RemoveRange(31, output.Count - 31);
    }

    /// <summary>Connected dead players in slot order, the local player included.</summary>
    internal void CopyDeadPlayersTo(List<SplitScreenDeadPlayer> output)
    {
        output.Clear();
        var round = StartOfRound.Instance;
        if (round == null || round.ClientPlayerList == null || round.allPlayerScripts == null) return;
        foreach (var entry in round.ClientPlayerList)
        {
            int slot = entry.Value;
            if (slot < 0 || slot >= round.allPlayerScripts.Length) continue;
            var player = round.allPlayerScripts[slot];
            if (player == null || player.actualClientId != entry.Key || player.disconnectedMidGame || !player.isPlayerDead) continue;
            var key = new SplitScreenKey(entry.Key, (ulong)slot);
            output.Add(new SplitScreenDeadPlayer(key, key, PlayerDisplayNames.Of(player), player.playerSteamId, player == round.localPlayerController));
        }
        output.Sort((a, b) => a.Key.SlotId.CompareTo(b.Key.SlotId));
    }

    /// <summary>How loudly the real player behind a view or bar entry speaks (Dissonance), 0 when silent.</summary>
    internal float VoiceLevel(SplitScreenKey source)
        => _voice.TryGetVoiceActivity(source.ClientId, source.SlotId, out var state) ? SplitScreenVoiceLevel.Intensity(state.IsSpeaking, state.Amplitude) : 0;

    internal ulong SteamIdOf(SplitScreenKey key)
    {
        var players = StartOfRound.Instance?.allPlayerScripts;
        return players != null && key.SlotId < (ulong)players.Length && players[key.SlotId] != null ? players[key.SlotId].playerSteamId : 0;
    }


    private sealed class BySlot : IComparer<SplitScreenParticipant>
    {
        internal static readonly BySlot Instance = new BySlot();
        public int Compare(SplitScreenParticipant a, SplitScreenParticipant b) => a.Key.SlotId.CompareTo(b.Key.SlotId);
    }

    internal static TMP_FontAsset? Font => HUDManager.Instance != null ? HUDManager.Instance.spectatingPlayerText?.font : null;
}

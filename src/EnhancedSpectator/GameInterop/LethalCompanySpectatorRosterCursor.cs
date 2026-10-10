using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The spectator pointer key is the top authority over the cursor while spectating (watch roster, split-screen):
/// once used, it shows the cursor or hides and locks it, and that state is re-asserted after every script and just
/// before rendering, whatever another mod does with the cursor. Only the game's own menus, chat and window focus
/// loss take precedence (and end the enforcement), so they stay usable.
/// </summary>
internal sealed class LethalCompanySpectatorRosterCursor
{
    private enum Owned { None, Shown, Hidden }
    internal static readonly LethalCompanySpectatorRosterCursor Shared = new();
    internal static bool SplitScreenOwnsInput { get; set; }
    private Owned _state;
    private bool _hooked;
    private CursorLockMode _savedLock;
    private bool _savedVisible;

    /// <summary><paramref name="toggle"/> switches between a shown pointer and a hidden, locked cursor.</summary>
    internal void Update(bool eligible, bool externalOwner, bool toggle, bool cancel)
    {
        if (!eligible || externalOwner || cancel) { Release(externalOwner); return; }
        if (toggle)
        {
            if (_state == Owned.None) { _savedLock = Cursor.lockState; _savedVisible = Cursor.visible; }
            _state = _state == Owned.Shown ? Owned.Hidden : Owned.Shown;
            SpectatorPointerCapture.IsActive = _state == Owned.Shown;
            if (!_hooked) { _hooked = true; Application.onBeforeRender += Assert; }
        }
        Assert();
    }

    private void Assert()
    {
        if (_state == Owned.None) return;
        bool shown = _state == Owned.Shown;
        Cursor.lockState = shown ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = shown;
    }

    internal void Release(bool externalOwner)
    {
        if (_hooked) { _hooked = false; Application.onBeforeRender -= Assert; }
        if (_state == Owned.None) return;
        bool shown = _state == Owned.Shown;
        _state = Owned.None;
        SpectatorPointerCapture.IsActive = false;
        // Restore only a cursor still in our shown state. Never overwrite a menu's cursor.
        if (shown && !externalOwner && Cursor.lockState == CursorLockMode.None && Cursor.visible)
        { Cursor.lockState = _savedLock; Cursor.visible = _savedVisible; }
    }
    internal static bool BlocksLook(PlayerControllerB player)
        => SpectatorPointerCapture.IsActive && StartOfRound.Instance != null
            && StartOfRound.Instance.localPlayerController == player && player.isPlayerDead;
}

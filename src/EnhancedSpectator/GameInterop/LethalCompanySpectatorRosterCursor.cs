using EnhancedSpectator.Features.Spectator;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>Owns only the temporary spectator pointer; menus and focus loss take precedence.</summary>
internal sealed class LethalCompanySpectatorRosterCursor
{
    private bool _owned;
    private CursorLockMode _savedLock;
    private bool _savedVisible;
    internal void Update(bool eligible, bool externalOwner, bool toggle, bool cancel)
    {
        if (!eligible || externalOwner || cancel) { Release(externalOwner); return; }
        if (toggle)
        {
            if (_owned) { Release(false); return; }
            _savedLock = Cursor.lockState; _savedVisible = Cursor.visible;
            _owned = SpectatorPointerCapture.IsActive = true;
        }
        if (_owned) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
    internal void Release(bool externalOwner)
    {
        if (!_owned) return;
        _owned = SpectatorPointerCapture.IsActive = false;
        // Restore only a cursor still in our state. Never overwrite a menu or another mod's takeover.
        if (!externalOwner && Cursor.lockState == CursorLockMode.None && Cursor.visible)
        { Cursor.lockState = _savedLock; Cursor.visible = _savedVisible; }
    }
    internal static bool BlocksLook(PlayerControllerB player)
        => SpectatorPointerCapture.IsActive && StartOfRound.Instance != null
            && StartOfRound.Instance.localPlayerController == player && player.isPlayerDead;
}

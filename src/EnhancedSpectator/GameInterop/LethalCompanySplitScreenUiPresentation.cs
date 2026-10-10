using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>Presentation only: keep the native backdrop and reports from covering an owned split-screen flow.</summary>
internal static class LethalCompanySplitScreenUiPresentation
{
    internal static RawImage? Surface;
    internal static bool ReportOwned;
    /// <summary>All crew dead: only the game's dialogue and game-over screen draw over the ship view.</summary>
    internal static bool Takeoff;
    /// <summary>
    /// A menu or the chat over the live split-screen (drawn beneath the game HUD then): of that HUD only the menu, the
    /// chat, the dialogue and the game-over texts draw; its clock, spectator UI and Advance_Features' boxes do not.
    /// </summary>
    internal static bool MenuOver;
    /// <summary>
    /// Our round report under a menu or the chat (drawn beneath the game HUD then): of that HUD only the menu, the
    /// dialogue and the chat while it is typed in (<see cref="ChatTyped"/>) draw over it. The player is revived by the
    /// time the days-left banner comes, and their status and old chat lines must not show through behind the menu.
    /// </summary>
    internal static bool ReportBeneath, ChatTyped;
    /// <summary>
    /// Revived out of the split-screen (the game's revive, e.g. its debug menu): the game's death screen plays its
    /// "revive" animation, fading its spectating UI (watched name, player boxes, vote) out over a moment, all of it
    /// covered by the split-screen until then. Until this time, while alive, that UI stays hidden.
    /// </summary>
    internal static float HideSpectateUiUntil = float.NegativeInfinity;
    private static readonly SpectatorCanvasRenderScope Scope = new();
    private static readonly HashSet<Canvas> Roots = new();

    /// <summary>The game's clock as its HUD shows it, another mod's format (BetterClock) included.</summary>
    internal static string ClockText => HUDManager.Instance.clockNumber.text;
    internal static Sprite ClockIcon => HUDManager.Instance.clockIcon.sprite;
    /// <summary>The day phase the clock icon shows (its place among the game's icons, as DayMode), or -1 for another mod's.</summary>
    internal static int ClockPhase => System.Array.IndexOf(HUDManager.Instance.clockIcons, HUDManager.Instance.clockIcon.sprite);
    /// <summary>The day's clock runs: landed (or taking off) on a moon with time; not in orbit or at the Company.</summary>
    internal static bool ClockRunning => StartOfRound.Instance is { inShipPhase: false } round && round.currentLevel.planetHasTime;
    private static readonly Transform[] TakeoffKeep = new Transform[2], MenuKeep = new Transform[5], MenuOnlyKeep = new Transform[3];

    internal static void Restore() => Scope.Restore();
    internal static void BeforeSubmit()
    {
        if (Surface != null)
            Scope.HideSurface(Surface.transform, Surface.canvas.rootCanvas.transform);
        var hud = HUDManager.Instance;
        if (hud == null) return;
        if (Time.unscaledTime < HideSpectateUiUntil && !StartOfRound.Instance.localPlayerController.isPlayerDead)
            Scope.HideBranch(hud.SpectateBoxesContainer.parent);
        if (Takeoff) HideAllButDialogue(hud);
        else if (MenuOver || ReportBeneath) HideHudButMenu(hud, MenuOver || ChatTyped);
        // A spectator's chat panel stands in for the game's chat box.
        if (LethalCompanyChat.VanillaHidden) { Scope.HideBranch(LethalCompanyChat.VanillaBox(hud)); Scope.HideBranch(hud.typingIndicator.transform); }
        if (!ReportOwned) return;
        if (hud.endgameStatsAnimator != null)
        {
            var stats = hud.endgameStatsAnimator.transform;
            Scope.HideBranch(stats);
            // Confirmed Advance_Features 1.3.0: Endscreen.Attach instantiates this prefab beside EndgameStats.
            // Suppress its presentation, while its animation, translated texts and game logic keep running.
            var advanced = stats.parent != null ? stats.parent.Find("PerformanceReport(Clone)") : null;
            if (advanced != null) Scope.HideBranch(advanced);
        }
        if (hud.gameOverAnimator != null) Scope.HideBranch(hud.gameOverAnimator.transform);
    }
    internal static void Clear()
    { Restore(); Surface = null; ReportOwned = ReportBeneath = ChatTyped = false; Takeoff = false; MenuOver = false; HideSpectateUiUntil = float.NegativeInfinity; }

    // Every canvas is looked up at submission, so another mod's overlay cannot slip in between: the game's clock,
    // Advance_Features' spectator boxes, chat lines and crosshairs all stay off the ship view.
    private static void HideAllButDialogue(HUDManager hud)
    {
        Roots.Clear();
        var split = Features.SplitScreen.SplitScreenModule.Current;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            var root = canvas.rootCanvas;
            if (!Roots.Add(root) || LethalCompanyCameraTransition.Owns(root) || split?.OwnsCanvas(root) == true) continue;
            // World-space canvases belong to the scene (terminal, signs), not to a HUD.
            if (root.renderMode == RenderMode.WorldSpace && root.worldCamera != hud.UICamera) continue;
            // The death screen also holds the spectator UI (DeathScreen/SpectateUI: its text, tips, the early-leave
            // vote and the player boxes Advance_Features restyles); only the game-over texts stay.
            TakeoffKeep[0] = hud.dialogueBoxAnimator.transform; TakeoffKeep[1] = hud.gameOverAnimator.transform;
            Scope.HideExcept(root, TakeoffKeep, hud.SpectateBoxesContainer.parent);
        }
    }

    // Only within the game's HUD canvas: other mods' menus (a config screen opened from the ESC menu) stay usable.
    private static void HideHudButMenu(HUDManager hud, bool chat)
    {
        var keep = chat ? MenuKeep : MenuOnlyKeep;
        keep[0] = StartOfRound.Instance.localPlayerController.quickMenuManager.menuContainer.transform;
        keep[1] = hud.dialogueBoxAnimator.transform; keep[2] = hud.gameOverAnimator.transform;
        if (chat) { keep[3] = hud.chatText.transform; keep[4] = hud.chatTextField.transform; }
        Scope.HideExcept(hud.chatText.canvas.rootCanvas, keep, hud.SpectateBoxesContainer.parent);
    }
}

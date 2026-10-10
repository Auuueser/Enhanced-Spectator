using System.Collections.Generic;
using BepInEx.Configuration;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorOptionsController
{
    /// <summary>Whether a row's setting differs from its default; view, HUD and action rows never count.</summary>
    public bool IsChanged(int row)
    {
        var c = _config.Camera; var s = c.SplitScreen;
        return row switch
        {
            1 => Changed(c.FirstPersonFov), 2 => Changed(c.FreecamDistance), 3 => Changed(_config.ThirdPersonDistance),
            4 => Changed(c.CinematicDistance), 5 => Changed(c.CinematicSpeed), 6 => Changed(_config.FearModelScaleMultiplier),
            7 => Changed(c.SharedModelScale), 8 => Changed(c.RetractAtWalls), 9 => Changed(c.FadeModelsNearby),
            10 => Changed(c.HearOtherFearSounds), 11 => Changed(c.RepairPlayerNames), 12 => Changed(c.ShowKeyHints),
            14 => Changed(c.FadeModelsWhileSpectating), 15 => Changed(c.FadeRadius), 16 => Changed(c.CinematicStyle),
            17 => Changed(c.FadeModelsNearOtherPlayers), 18 => Changed(c.MonitorStyle), 19 => Changed(c.StabilizeFollow),
            20 => Changed(c.BalanceSpectatorBrightness), 21 => Changed(c.SpectatorBrightness), 22 => Changed(c.AutoCenter),
            23 => Changed(c.FollowSpeed), 24 => Changed(c.HideAllModels), 25 => Changed(c.ModelDisplayLimit),
            26 => Changed(c.HideAutoCenteringModels), 27 => Changed(c.MonitorInfrared), 28 => Changed(c.AutoMonitorIndoors),
            29 => Changed(c.ShowSpectatorRoster), 30 => Changed(s.AutoEnable), 31 => Changed(s.Quality), 32 => Changed(s.MainScale),
            33 => Changed(s.MainFps), 34 => Changed(s.TileScale), 35 => Changed(s.TileFps), 38 => Changed(s.MainFollowsGame),
            39 => Changed(s.StartFocused), 40 => Changed(c.RevealSpeakingCenteringModels), 41 => Changed(s.ThermalAllViews),
            42 => Changed(s.ShowSpeaking), 43 => Changed(s.ShowDeadBar), 46 => Changed(s.ShowViewerCount), 48 => Changed(s.StyledReport), 49 => Changed(s.ShowClock), 50 => Changed(s.ChatPopup), 47 => Changed(c.EnableAudienceBets), 44 => Changed(c.ThermalPalette), 45 => Changed(c.ThermalStrength),
            _ => false
        };
    }
    private static bool Changed<T>(ConfigEntry<T> entry) => !EqualityComparer<T>.Default.Equals(entry.Value, (T)entry.DefaultValue);

    /// <summary>Rows of a page in display order, optionally only the changed ones.</summary>
    public void CopyPageRows(int page, bool changedOnly, List<int> output)
    {
        output.Clear();
        for (int row = 0; row < RowCount; row++)
            if (PageForRow(row) == page && (!changedOnly || IsChanged(row))) output.Add(row);
        output.Sort((a, b) => DisplayOrder(a).CompareTo(DisplayOrder(b)));
    }
    // Related settings sit together: thermal colours and intensity follow the thermal switch; the split-screen
    // page lists rendering first, then layout, display and its two actions.
    private static readonly int[] SplitOrder = { 31, 38, 32, 33, 34, 35, 41, 30, 39, 42, 43, 46, 49, 50, 48, 36, 37 };
    private static float DisplayOrder(int row)
        => row == 44 ? 27.1f : row == 45 ? 27.2f : PageForRow(row) == 4 ? 100 + System.Array.IndexOf(SplitOrder, row) : row;
}

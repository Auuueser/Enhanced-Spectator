using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;

namespace EnhancedSpectator.Features.SplitScreen;

internal readonly struct SplitScreenRenderBudget
{
    internal readonly float MainScale, TileScale;
    // TileCascades: outdoor sun shadow cascades for small views; 0 keeps the game's own setting.
    internal readonly int MainFps, TileFps, TileDraws, TileCascades;
    internal SplitScreenRenderBudget(float mainScale, int mainFps, float tileScale, int tileFps, int tileDraws, int tileCascades)
    { MainScale = mainScale; MainFps = mainFps; TileScale = tileScale; TileFps = tileFps; TileDraws = tileDraws; TileCascades = tileCascades; }

    /// <summary>Auto's budget tier: 0 quality for up to 3 views, 1 balanced up to 12, 2 performance beyond.</summary>
    internal static int AutoTier(int count) => count <= 3 ? 0 : count <= 12 ? 1 : 2;

    // mainFollowsGame: Quality and Auto draw the large view every game frame; Performance and Custom keep their limits.
    internal static SplitScreenRenderBudget Resolve(SplitScreenQuality quality, int count,
        float mainScale = .9f, int mainFps = 75, float tileScale = .8f, int tileFps = 30, bool mainFollowsGame = true)
    {
        int tier = count <= 4 ? 0 : count <= 9 ? 1 : count <= 16 ? 2 : 3;
        int budgetTier = quality == SplitScreenQuality.Auto ? AutoTier(count) : quality == SplitScreenQuality.Quality ? 0 : 2;
        var budget = quality == SplitScreenQuality.Custom
            ? new SplitScreenRenderBudget(mainScale, NormalizeFps(mainFps), tileScale, NormalizeFps(tileFps), 10, 0)
            : budgetTier switch
            {
                0 => new SplitScreenRenderBudget(1f, 75, 1f, tier == 0 ? 60 : tier == 1 ? 40 : tier == 2 ? 25 : 15, 10, 0),
                1 => new SplitScreenRenderBudget(.9f, 75, .8f, tier == 0 ? 45 : tier == 1 ? 30 : tier == 2 ? 20 : 12, 6, 2),
                _ => new SplitScreenRenderBudget(.75f, 45, .6f, tier == 0 ? 24 : tier == 1 ? 15 : 10, 5, 1)
            };
        bool follows = count <= 1 || mainFollowsGame && (quality == SplitScreenQuality.Auto || quality == SplitScreenQuality.Quality);
        if (follows) budget = new SplitScreenRenderBudget(budget.MainScale, 0, budget.TileScale, budget.TileFps, budget.TileDraws, budget.TileCascades);
        // The last player left is an ordinary spectator view: it follows the game's frame rate and shadows.
        return count <= 1 ? new SplitScreenRenderBudget(budget.MainScale, 0, budget.MainScale, 0, budget.TileDraws, 0) : budget;
    }

    /// <summary>Preset name for the settings page and preview toolbar; Auto also names the budget tier it currently uses.</summary>
    internal static string PresetName(SplitScreenQuality quality, int count, bool chinese)
    {
        var names = chinese ? ChineseNames : EnglishNames;
        if (quality != SplitScreenQuality.Auto) return names[(int)quality];
        return names[(int)quality] + " · " + (chinese ? ChineseTiers : EnglishTiers)[AutoTier(count)];
    }
    private static readonly string[] ChineseNames = { "性能", "自适应", "质量", "自定义" },
        EnglishNames = { "Performance", "Auto", "Quality", "Custom" },
        ChineseTiers = { "质量", "平衡", "性能" }, EnglishTiers = { "quality", "balanced", "performance" };

    internal static int NormalizeFps(int fps) => fps == 0 ? 0 : Math.Max(5, Math.Min(120, fps));
    internal static void Size(float displayedHeight, float scale, out int width, out int height)
    {
        int units = Math.Max(4, (int)Math.Ceiling(displayedHeight * scale / 9));
        width = units * 16; height = units * 9;
    }
}

// The oldest due view wins. Missed frames are discarded, never replayed in a burst.
internal sealed class SplitScreenRenderSchedule
{
    private readonly Dictionary<SplitScreenKey, double> _due = new();
    private readonly HashSet<SplitScreenKey> _selected = new();

    internal void Select(IReadOnlyList<SplitScreenParticipant> players, SplitScreenKey? focused,
        SplitScreenRenderBudget budget, double now, List<SplitScreenKey> output)
    {
        output.Clear(); _selected.Clear();
        if (focused.HasValue && IsDue(focused.Value, now))
        {
            output.Add(focused.Value); _selected.Add(focused.Value);
            SetNext(focused.Value, now, budget.MainFps);
        }
        for (int draw = 0; draw < budget.TileDraws; draw++)
        {
            SplitScreenKey? oldest = null;
            double time = double.PositiveInfinity;
            foreach (var player in players)
            {
                if (player.Key == focused || _selected.Contains(player.Key)) continue;
                double due = _due.TryGetValue(player.Key, out var scheduled) ? scheduled : double.NegativeInfinity;
                if (due <= now + .000001 && (!oldest.HasValue || due < time)) { oldest = player.Key; time = due; }
            }
            if (!oldest.HasValue) break;
            output.Add(oldest.Value); _selected.Add(oldest.Value);
            SetNext(oldest.Value, now, budget.TileFps);
        }
    }

    private bool IsDue(SplitScreenKey key, double now) => !_due.TryGetValue(key, out var due) || due <= now + .000001;
    private void SetNext(SplitScreenKey key, double now, int fps) => _due[key] = fps == 0 ? now : now + 1d / fps;
    internal void Invalidate(SplitScreenKey key) => _due.Remove(key);
    internal void Remove(SplitScreenKey key) => _due.Remove(key);
    internal void Clear() { _due.Clear(); _selected.Clear(); }
}

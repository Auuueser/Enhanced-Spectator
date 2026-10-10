using System;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.SplitScreen;

namespace EnhancedSpectator.Features.Spectator;

public sealed partial class SpectatorOptionsController
{
    private string DescribeSplitScreen(int row, bool chinese)
    {
        var c = _config.Camera.SplitScreen;
        // Outside a split-screen the page describes a typical four-view session.
        int count = SplitScreenModule.Current?.Active == true ? SplitScreenModule.Current.ParticipantCount : 4;
        var budget = SplitScreenRenderBudget.Resolve(c.Quality.Value, count, c.MainScale.Value, c.MainFps.Value, c.TileScale.Value, c.TileFps.Value,
            c.MainFollowsGame.Value);
        string Fps(int fps) => fps == 0 ? chinese ? "跟随游戏" : "Game FPS" : fps + " FPS";
        return row switch
        {
            30 => chinese ? "死亡后自动开启分屏" : "Split-screen automatically after death",
            31 => (chinese ? "渲染档位：" : "Preset: ") + SplitScreenRenderBudget.PresetName(c.Quality.Value, count, chinese),
            32 => (chinese ? "大窗分辨率：" : "Focused resolution: ") + $"{budget.MainScale:P0} · {SplitScreenModule.Current?.ResolutionText(true, budget.MainScale)}",
            33 => (chinese ? "大窗刷新上限：" : "Focused refresh limit: ") + Fps(budget.MainFps),
            34 => (chinese ? "小窗分辨率：" : "Small view resolution: ") + $"{budget.TileScale:P0} · {SplitScreenModule.Current?.ResolutionText(false, budget.TileScale)}",
            35 => (chinese ? "小窗刷新上限：" : "Small view refresh limit: ") + Fps(budget.TileFps),
            36 => chinese ? "本次观战分屏" : "Split-screen for this life",
            38 => (chinese ? "大窗跟随游戏帧率（自适应／质量）：" : "Large view at game FPS (Auto/Quality): ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            39 => (chinese ? "开启时大窗＋侧边小窗：" : "Open with a large view: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            41 => (chinese ? "全部窗口热成像：" : "Thermal in every view: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            42 => (chinese ? "说话时名牌闪动：" : "Flash speaking name plates: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            43 => (chinese ? "阵亡玩家栏：" : "Dead player bar: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            46 => (chinese ? "大窗显示观看人数：" : "Viewer count on large view: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            49 => (chinese ? "分屏时钟：" : "Split-screen clock: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            50 => (chinese ? "新聊天消息弹出：" : "Pop up new chat messages: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off"),
            48 => (chinese ? "回合结算界面：" : "Round report: ") + (chinese ? ToggleValue(row) ? "分屏风格" : "游戏原版" : ToggleValue(row) ? "Split-screen style" : "Game's own"),
            _ => chinese ? "局内交互测试（单人可用）" : "Interactive test (solo supported)"
        };
    }
    internal bool CanAdjustSplitScreen(int row) => row < 32 || row > 35 || _config.Camera.SplitScreen.Quality.Value == SplitScreenQuality.Custom;
    private void AdjustSplitScreen(int row, int direction)
    {
        var c = _config.Camera.SplitScreen;
        if (!CanAdjustSplitScreen(row)) return;
        int NextFps(int value) { int next = value + Math.Sign(direction) * 5; return next < 0 ? 120 : next > 120 ? 0 : next; }
        switch (row)
        {
            case 30: c.AutoEnable.Value = !c.AutoEnable.Value; break;
            case 31: c.Quality.Value = (SplitScreenQuality)(((int)c.Quality.Value + Math.Sign(direction) + 4) % 4); break;
            case 32: c.MainScale.Value = Math.Max(.25f, Math.Min(1, c.MainScale.Value + Math.Sign(direction) * .05f)); break;
            case 33: c.MainFps.Value = NextFps(c.MainFps.Value); break;
            case 34: c.TileScale.Value = Math.Max(.25f, Math.Min(1, c.TileScale.Value + Math.Sign(direction) * .05f)); break;
            case 35: c.TileFps.Value = NextFps(c.TileFps.Value); break;
            case 36: SplitScreenModule.Current?.RequestToggle(); break;
            case 37: SplitScreenModule.Current?.RequestPreview(); break;
            case 38: c.MainFollowsGame.Value = !c.MainFollowsGame.Value; break;
            case 39: c.StartFocused.Value = !c.StartFocused.Value; break;
            case 41: c.ThermalAllViews.Value = !c.ThermalAllViews.Value; break;
            case 42: c.ShowSpeaking.Value = !c.ShowSpeaking.Value; break;
            case 43: c.ShowDeadBar.Value = !c.ShowDeadBar.Value; break;
            case 46: c.ShowViewerCount.Value = !c.ShowViewerCount.Value; break;
            case 48: c.StyledReport.Value = !c.StyledReport.Value; break;
            case 49: c.ShowClock.Value = !c.ShowClock.Value; break;
            case 50: c.ChatPopup.Value = !c.ChatPopup.Value; break;
        }
    }
    internal void ResetSplitScreen()
    {
        var c = _config.Camera.SplitScreen;
        c.AutoEnable.Value = true; c.StartFocused.Value = true; c.MainFollowsGame.Value = true; c.ThermalAllViews.Value = false; c.ShowSpeaking.Value = true; c.ShowDeadBar.Value = true; c.ShowViewerCount.Value = true; c.StyledReport.Value = true; c.ShowClock.Value = false; c.ChatPopup.Value = true; c.Quality.Value = SplitScreenQuality.Auto;
        c.MainScale.Value = .9f; c.TileScale.Value = .8f; c.MainFps.Value = 75; c.TileFps.Value = 30;
    }
}

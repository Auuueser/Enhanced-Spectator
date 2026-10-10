using System.Collections.Generic;
using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>
/// Key hints written for the current state: a first row says where the player is (no key), and every other row says
/// what its key does now ("返回平铺" while a view is enlarged, "关闭热成像" while thermal is on). The camera the player
/// is already using is left out of the camera keys; the choreography camera is offered indoors only.
/// </summary>
internal static class SpectatorHintText
{
    internal readonly struct Row
    {
        internal readonly string Label, Keys;
        internal Row(string label, string keys) { Label = label; Keys = keys; }
    }

    internal static string ModeName(SpectatorCameraMode? mode, bool chinese) => mode switch
    {
        null => chinese ? "原版" : "Vanilla",
        SpectatorCameraMode.Freecam => chinese ? "自由视角" : "Free camera",
        SpectatorCameraMode.ThirdPerson => chinese ? "第三人称" : "Third person",
        SpectatorCameraMode.FirstPerson => chinese ? "第一人称" : "First person",
        SpectatorCameraMode.Cinematic => chinese ? "电影" : "Cinema",
        SpectatorCameraMode.Monitor => chinese ? "运镜模式" : "Camera choreography",
        _ => chinese ? "导演镜头" : "Director",
    };

    internal static List<Row> Rows(EnhancedSpectatorConfig c, SpectatorCameraMode? mode, bool fearAllowed)
    {
        bool cn = c.UseChineseText;
        string Key(KeyCode key) => cn ? key switch {
            KeyCode.LeftArrow => "左", KeyCode.RightArrow => "右", KeyCode.UpArrow => "上", KeyCode.DownArrow => "下",
            _ => SpectatorHotkeySettings.KeyLabel(key, true)
        } : SpectatorHotkeySettings.KeyLabel(key);
        var rows = new List<Row>();
        void Status(string zh, string en) => rows.Add(new Row(cn ? zh : en, string.Empty));
        void Single(string zh, string en, KeyCode key) { if (key != KeyCode.None) rows.Add(new Row(cn ? zh : en, Key(key))); }
        void Pair(string zhA, string enA, KeyCode a, string zhB, string enB, KeyCode b)
        {
            if (a == KeyCode.None) { Single(zhB, enB, b); return; }
            if (b == KeyCode.None) { Single(zhA, enA, a); return; }
            rows.Add(new Row(cn ? zhA + " / " + zhB : enA + " / " + enB, Key(a) + " / " + Key(b)));
        }
        // The cameras other than the one in use (with none in use, all of them), in their usual pairs.
        void Cameras(bool inUse, SpectatorCameraMode? current, bool monitorAvailable)
        {
            KeyCode Offer(SpectatorCameraMode? camera, KeyCode key, bool enabled = true) => enabled && !(inUse && camera == current) ? key : KeyCode.None;
            Pair("自由视角", "Free camera", Offer(SpectatorCameraMode.Freecam, c.ToggleFreecamKey.Value, c.EnableFreecam.Value),
                "原版", "Vanilla", Offer(null, c.ResetToVanillaViewKey.Value));
            Pair("第三人称", "Third person", Offer(SpectatorCameraMode.ThirdPerson, c.ToggleThirdPersonKey.Value, c.EnableThirdPerson.Value),
                "第一人称", "First person", Offer(SpectatorCameraMode.FirstPerson, c.Camera.FirstPersonKey.Value));
            Pair("电影", "Cinema", Offer(SpectatorCameraMode.Cinematic, c.Camera.CinematicKey.Value),
                "运镜模式", "Camera choreography", Offer(SpectatorCameraMode.Monitor, c.Camera.MonitorKey.Value, monitorAvailable));
        }
        // Left/Right pick the cinematic or choreography style.
        void Styles(SpectatorCameraMode? styled)
        {
            if (styled == SpectatorCameraMode.Cinematic)
                rows.Add(new Row((cn ? "风格：" : "Style: ") + CinematicStyles.Name(c.Camera.CinematicStyle.Value, cn), cn ? "左 / 右" : "Left / Right"));
            else if (styled == SpectatorCameraMode.Monitor)
                rows.Add(new Row((cn ? "风格：" : "Style: ") + MonitorCameraStyles.Name(SpectatorFreecamController.Current?.EffectiveMonitorStyle ?? c.Camera.MonitorStyle.Value, cn), cn ? "左 / 右" : "Left / Right"));
        }
        // Thermal on or off, and Up/Down for its colours while it is on.
        void Thermal()
        {
            bool on = c.Camera.MonitorInfrared.Value;
            Single(on ? "关闭热成像" : "开启热成像", on ? "Thermal off" : "Thermal on", c.Camera.MonitorInfraredKey.Value);
            if (on) rows.Add(new Row((cn ? "热成像配色：" : "Thermal colours: ") + ThermalPalettes.Name(c.Camera.ThermalPalette.Value, cn), cn ? "上 / 下" : "Up / Down"));
        }
        void Movement()
        {
            rows.Add(new Row(cn ? "移动" : "Move", $"{Key(c.Camera.MoveForwardKey.Value)}/{Key(c.Camera.MoveLeftKey.Value)}/{Key(c.Camera.MoveBackKey.Value)}/{Key(c.Camera.MoveRightKey.Value)}"));
            Pair("上升", "Up", c.AscendKey.Value, "下降", "Down", c.DescendKey.Value);
        }
        bool pointerFree = SpectatorPointerCapture.IsActive;

        if (SplitScreen.SplitScreenModule.Current is { Active: true } module)
        {
            bool large = module.LargeViewOpen;
            var split = module.EffectiveMode;
            if (large) Status("当前：放大 · " + ModeName(split, true), "Now: enlarged · " + ModeName(split, false));
            else Status("当前：平铺", "Now: tiled");
            rows.Add(large ? new Row(cn ? "返回平铺" : "Back to tiles", cn ? "点击窗口" : "Click a view")
                : new Row(cn ? "放大窗口（观看该玩家）" : "Enlarge (watch that player)", cn ? "点击窗口" : "Click a view"));
            // The same keys work in the preview; the watch roster belongs to the single view, not the split-screen.
            Single("关闭分屏", "Close split-screen", c.Camera.SplitScreen.ToggleKey.Value);
            // A camera key with every view tiled enlarges the audio view with that camera.
            Cameras(large, split, module.MonitorAvailable);
            // Mouse and movement drive the free and third-person cameras once the pointer is handed over.
            Single(pointerFree ? "操控镜头" : "显示光标", pointerFree ? "Control the camera" : "Show pointer", c.Camera.ToggleSpectatorCursorKey.Value);
            if (large)
            {
                Styles(split);
                if (split != SpectatorCameraMode.FirstPerson && split != SpectatorCameraMode.Monitor && split != SpectatorCameraMode.Director)
                    rows.Add(new Row(cn ? "观战距离" : "Target distance", cn ? "滚轮" : "Wheel"));
                if (split == SpectatorCameraMode.Freecam || split == SpectatorCameraMode.ThirdPerson) Movement();
            }
            Single("隐藏界面", "Hide HUD", c.Camera.ToggleHudKey.Value);
            bool clock = c.Camera.SplitScreen.ShowClock.Value;
            Single(clock ? "隐藏时钟" : "显示时钟", clock ? "Hide clock" : "Show clock", c.Camera.SplitScreen.ClockKey.Value);
            Thermal();
            if (Social.SpectatorSocialAvailability.Emotes) Single("观战表情", "Emotes", c.Camera.EmoteKey.Value);
            if (Social.SpectatorSocialAvailability.Bets) Single("观众竞猜", "Audience bets", c.Camera.BetKey.Value);
            if (Social.SpectatorSocialAvailability.Reviews) Single("评价队友", "Rate teammates", c.Camera.RateKey.Value);
            return rows;
        }

        Status("当前：" + ModeName(mode, true), "Now: " + ModeName(mode, false));
        Single("开启分屏", "Open split-screen", c.Camera.SplitScreen.ToggleKey.Value);
        Cameras(true, mode, SpectatorFreecamController.Current?.CanUseMonitorView == true);
        if (mode.HasValue) Single("回正视角", "Recenter", c.RecenterKey.Value);
        Styles(mode);
        Thermal();
        if (mode != SpectatorCameraMode.FirstPerson && mode != SpectatorCameraMode.Monitor && mode != SpectatorCameraMode.Director)
            rows.Add(new Row(cn ? "观战距离" : "Target distance", cn ? "滚轮" : "Wheel"));
        if (mode == SpectatorCameraMode.ThirdPerson && c.Camera.SelfCameraDistanceModifier.Value != KeyCode.None)
            rows.Add(new Row(cn ? "自身镜头距离" : "Self camera", Key(c.Camera.SelfCameraDistanceModifier.Value) + (cn ? " + 滚轮" : " + Wheel")));
        if (mode == SpectatorCameraMode.Freecam || mode == SpectatorCameraMode.ThirdPerson)
        {
            Movement();
            Pair("加速", "Fast", c.FastMoveKey.Value, "减速", "Slow", c.SlowMoveKey.Value);
        }
        if (fearAllowed)
        {
            Pair("上一模型", "Previous model", CinematicStyles.ReservesKey(mode, c.FearModelPreviousKey.Value) ? KeyCode.None : c.FearModelPreviousKey.Value,
                "下一模型", "Next model", CinematicStyles.ReservesKey(mode, c.FearModelNextKey.Value) ? KeyCode.None : c.FearModelNextKey.Value);
            Pair("播放/停止", "Play/stop", c.FearSoundKey.Value, "下一音效", "Next sound", c.FearSoundNextKey.Value);
        }
        Pair("观战表情", "Emotes", Social.SpectatorSocialAvailability.Emotes ? c.Camera.EmoteKey.Value : KeyCode.None, "观众竞猜", "Audience bets", Social.SpectatorSocialAvailability.Bets ? c.Camera.BetKey.Value : KeyCode.None);
        if (Social.SpectatorSocialAvailability.Reviews) Single("评价队友", "Rate teammates", c.Camera.RateKey.Value);
        // These rows are read while the hints show.
        Single("隐藏按键提示", "Hide key hints", c.Camera.ToggleKeyHintsKey.Value);
        bool roster = c.Camera.ShowSpectatorRoster.Value;
        Single(roster ? "隐藏观战面板" : "显示观战面板", roster ? "Hide watch panel" : "Show watch panel", c.Camera.ToggleSpectatorRosterKey.Value);
        Single(pointerFree ? "隐藏光标" : "显示光标（查看观众）", pointerFree ? "Hide pointer" : "Show pointer (viewers)", c.Camera.ToggleSpectatorCursorKey.Value);
        Single("隐藏界面", "Hide HUD", c.Camera.ToggleHudKey.Value);
        return rows;
    }

    internal static string Build(EnhancedSpectatorConfig c, SpectatorCameraMode? mode, bool fearAllowed)
    {
        var lines = new List<string>();
        foreach (var row in Rows(c, mode, fearAllowed)) lines.Add(row.Keys.Length == 0 ? row.Label : row.Label + " [" + row.Keys + "]");
        return string.Join("\n", lines);
    }
}

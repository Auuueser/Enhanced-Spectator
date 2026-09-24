using System.Collections.Generic;
using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

internal static class SpectatorHintText
{
    internal readonly struct Row
    {
        internal readonly string Label, Keys;
        internal Row(string label, string keys) { Label = label; Keys = keys; }
    }
    internal static List<Row> Rows(EnhancedSpectatorConfig c, SpectatorCameraMode? mode, bool fearAllowed)
    {
        bool cn = c.UseChineseText;
        string Key(KeyCode key) => cn ? key switch {
            KeyCode.LeftArrow => "左", KeyCode.RightArrow => "右", KeyCode.UpArrow => "上", KeyCode.DownArrow => "下",
            _ => SpectatorHotkeySettings.KeyLabel(key, true)
        } : SpectatorHotkeySettings.KeyLabel(key);
        var rows = new List<Row>();
        void Single(string zh, string en, KeyCode key) { if (key != KeyCode.None) rows.Add(new Row(cn ? zh : en, Key(key))); }
        void Pair(string zhA, string enA, KeyCode a, string zhB, string enB, KeyCode b)
        {
            if (a == KeyCode.None) { Single(zhB, enB, b); return; }
            if (b == KeyCode.None) { Single(zhA, enA, a); return; }
            rows.Add(new Row(cn ? zhA + " / " + zhB : enA + " / " + enB, Key(a) + " / " + Key(b)));
        }
        Pair("自由", "Free", c.EnableFreecam.Value ? c.ToggleFreecamKey.Value : KeyCode.None, "原版", "Vanilla", c.ResetToVanillaViewKey.Value);
        Pair("第三人称", "Third", c.EnableThirdPerson.Value ? c.ToggleThirdPersonKey.Value : KeyCode.None, "第一人称", "First", c.Camera.FirstPersonKey.Value);
        Pair("电影", "Cinema", c.Camera.CinematicKey.Value, "回正", "Recenter", mode.HasValue ? c.RecenterKey.Value : KeyCode.None);
        if (mode == SpectatorCameraMode.Cinematic)
            rows.Add(new Row((cn ? "风格：" : "Style: ") + CinematicStyles.Name(c.Camera.CinematicStyle.Value, cn), cn ? "左 / 右" : "Left / Right"));
        if (mode != SpectatorCameraMode.FirstPerson) rows.Add(new Row(cn ? "观战距离" : "Target distance", cn ? "滚轮" : "Wheel"));
        if (mode == SpectatorCameraMode.ThirdPerson && c.Camera.SelfCameraDistanceModifier.Value != KeyCode.None)
            rows.Add(new Row(cn ? "自身镜头距离" : "Self camera", Key(c.Camera.SelfCameraDistanceModifier.Value) + (cn ? " + 滚轮" : " + Wheel")));
        if (mode == SpectatorCameraMode.Freecam || mode == SpectatorCameraMode.ThirdPerson)
        {
            rows.Add(new Row(cn ? "移动" : "Move", $"{Key(c.Camera.MoveForwardKey.Value)}/{Key(c.Camera.MoveLeftKey.Value)}/{Key(c.Camera.MoveBackKey.Value)}/{Key(c.Camera.MoveRightKey.Value)}"));
            Pair("上升", "Up", c.AscendKey.Value, "下降", "Down", c.DescendKey.Value);
            Pair("加速", "Fast", c.FastMoveKey.Value, "减速", "Slow", c.SlowMoveKey.Value);
        }
        if (fearAllowed)
        {
            Pair("上一模型", "Previous model", CinematicStyles.ReservesKey(mode, c.FearModelPreviousKey.Value) ? KeyCode.None : c.FearModelPreviousKey.Value,
                "下一模型", "Next model", CinematicStyles.ReservesKey(mode, c.FearModelNextKey.Value) ? KeyCode.None : c.FearModelNextKey.Value);
            Pair("播放/停止", "Play/stop", c.FearSoundKey.Value, "下一音效", "Next sound", c.FearSoundNextKey.Value);
        }
        Single("按键提示", "Key hints", c.Camera.ToggleKeyHintsKey.Value);
        Single("隐藏界面", "Hide HUD", c.Camera.ToggleHudKey.Value);
        return rows;
    }
    internal static string Build(EnhancedSpectatorConfig c, SpectatorCameraMode? mode, bool fearAllowed)
    {
        var lines = new List<string>();
        foreach (var row in Rows(c, mode, fearAllowed)) lines.Add(row.Label + " [" + row.Keys + "]");
        return string.Join("\n", lines);
    }
}

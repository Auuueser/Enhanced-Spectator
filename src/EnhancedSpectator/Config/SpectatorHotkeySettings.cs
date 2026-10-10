using System;
using System.Collections.Generic;
using EnhancedSpectator.Logging;
using BepInEx.Configuration;
using EnhancedSpectator.Features.Spectator;
using UnityEngine;

namespace EnhancedSpectator.Config;

internal sealed class SpectatorHotkeyBinding
{
    internal readonly string Chinese, English;
    internal readonly ConfigEntry<KeyCode> Entry;
    internal readonly bool WheelModifier, SpeedModifier;
    internal SpectatorHotkeyBinding(string chinese, string english, ConfigEntry<KeyCode> entry, bool wheel = false, bool speed = false)
    { Chinese = chinese; English = english; Entry = entry; WheelModifier = wheel; SpeedModifier = speed; }
}

/// <summary>Shared persistent bindings for options, input and spectator hints.</summary>
internal sealed class SpectatorHotkeySettings
{
    internal readonly SpectatorHotkeyBinding[] Bindings;
    internal SpectatorHotkeySettings(EnhancedSpectatorConfig c)
    {
        Bindings = new[] {
            new SpectatorHotkeyBinding("增强自由视角", "Freecam", c.ToggleFreecamKey),
            new SpectatorHotkeyBinding("返回原版观战", "Vanilla view", c.ResetToVanillaViewKey),
            new SpectatorHotkeyBinding("自身第三人称", "Self third person", c.ToggleThirdPersonKey),
            new SpectatorHotkeyBinding("队友第一人称", "Player first person", c.Camera.FirstPersonKey),
            new SpectatorHotkeyBinding("电影跟拍", "Cinematic", c.Camera.CinematicKey),
            new SpectatorHotkeyBinding("重新对准目标", "Recenter", c.RecenterKey),
            new SpectatorHotkeyBinding("自身镜头距离（按住＋滚轮）", "Self camera (hold + wheel)", c.Camera.SelfCameraDistanceModifier, wheel: true),
            new SpectatorHotkeyBinding("前进", "Forward", c.Camera.MoveForwardKey),
            new SpectatorHotkeyBinding("后退", "Backward", c.Camera.MoveBackKey),
            new SpectatorHotkeyBinding("向左", "Left", c.Camera.MoveLeftKey),
            new SpectatorHotkeyBinding("向右", "Right", c.Camera.MoveRightKey),
            new SpectatorHotkeyBinding("上升", "Ascend", c.AscendKey),
            new SpectatorHotkeyBinding("下降", "Descend", c.DescendKey),
            new SpectatorHotkeyBinding("加速", "Move faster", c.FastMoveKey, speed: true),
            new SpectatorHotkeyBinding("减速", "Move slower", c.SlowMoveKey, speed: true),
            new SpectatorHotkeyBinding("上一个模型", "Previous model", c.FearModelPreviousKey),
            new SpectatorHotkeyBinding("下一个模型", "Next model", c.FearModelNextKey),
            new SpectatorHotkeyBinding("播放／停止恐惧音效", "Play / stop fear sound", c.FearSoundKey),
            new SpectatorHotkeyBinding("下一恐惧音效", "Next fear sound", c.FearSoundNextKey),
            new SpectatorHotkeyBinding("显示／隐藏按键提示", "Toggle key hints", c.Camera.ToggleKeyHintsKey),
            new SpectatorHotkeyBinding("隐藏／显示观战界面", "Toggle spectator HUD", c.Camera.ToggleHudKey),
            new SpectatorHotkeyBinding("运镜模式视角", "Camera choreography view", c.Camera.MonitorKey),
            new SpectatorHotkeyBinding("开关红外热成像", "Toggle thermal imaging", c.Camera.MonitorInfraredKey),
            new SpectatorHotkeyBinding("显示／隐藏观战面板", "Toggle watch panel", c.Camera.ToggleSpectatorRosterKey),
            new SpectatorHotkeyBinding("观战光标／查看观众", "Viewer-list pointer", c.Camera.ToggleSpectatorCursorKey),
            new SpectatorHotkeyBinding("开关观战分屏", "Toggle split-screen", c.Camera.SplitScreen.ToggleKey),
            new SpectatorHotkeyBinding("显示／隐藏分屏时钟", "Toggle split-screen clock", c.Camera.SplitScreen.ClockKey),
            new SpectatorHotkeyBinding("显示／隐藏分屏测试面板", "Toggle split-screen preview panel", c.Camera.SplitScreen.PreviewPanelKey),
            new SpectatorHotkeyBinding("观战表情", "Spectator emotes", c.Camera.EmoteKey),
            new SpectatorHotkeyBinding("观众竞猜", "Audience bets", c.Camera.BetKey),
            new SpectatorHotkeyBinding("评价队友", "Rate teammates", c.Camera.RateKey)
        };
    }
    internal bool TryAssign(int index, KeyCode key, out int conflict)
    {
        conflict = -1;
        if (index < 0 || index >= Bindings.Length || key == KeyCode.Escape || key == KeyCode.Backspace
            || (key != KeyCode.None && !SpectatorInputKeyMappings.Resolve(key).IsMapped)) return false;
        // Arrow keys remain contextual: styles in cinema, model selection elsewhere.
        if (CinematicStyles.IsStyleKey(key) && index != 15 && index != 16) return false;
        if (key != KeyCode.None)
            for (int i = 0; i < Bindings.Length; ++i)
            {
                if (i == index || Canonical(Bindings[i].Entry.Value) != Canonical(key)) continue;
                if ((Bindings[index].WheelModifier && Bindings[i].SpeedModifier)
                    || (Bindings[i].WheelModifier && Bindings[index].SpeedModifier)) continue;
                conflict = i; return false;
            }
        var entry = Bindings[index].Entry;
        var old = entry.Value;
        bool autosave = entry.ConfigFile.SaveOnConfigSet;
        entry.ConfigFile.SaveOnConfigSet = false;
        try
        {
            entry.Value = key;
            // Explicitly persist the file that owns this binding, even if another config UI disables autosave.
            entry.ConfigFile.Save();
            return true;
        }
        catch (Exception ex)
        {
            entry.Value = old;
            conflict = -2;
            ModLog.Warning($"Could not save spectator hotkey: {ex.GetType().Name}.");
            return false;
        }
        finally { entry.ConfigFile.SaveOnConfigSet = autosave; }
    }
    internal void Reset()
    {
        var files = new Dictionary<ConfigFile, bool>();
        foreach (var b in Bindings) if (!files.ContainsKey(b.Entry.ConfigFile)) files.Add(b.Entry.ConfigFile, b.Entry.ConfigFile.SaveOnConfigSet);
        try
        {
            foreach (var file in files.Keys) file.SaveOnConfigSet = false;
            foreach (var b in Bindings) b.Entry.BoxedValue = b.Entry.DefaultValue;
        }
        finally { foreach (var pair in files) { pair.Key.SaveOnConfigSet = pair.Value; pair.Key.Save(); } }
    }
    private static KeyCode Canonical(KeyCode key) => key switch {
        KeyCode.RightAlt => KeyCode.LeftAlt, KeyCode.RightControl => KeyCode.LeftControl, KeyCode.RightShift => KeyCode.LeftShift, _ => key
    };
    internal static string KeyLabel(KeyCode key, bool chinese = false) => Canonical(key) switch {
        KeyCode.None => chinese ? "未绑定" : "Unbound", KeyCode.LeftAlt => "Alt", KeyCode.LeftControl => "Ctrl", KeyCode.LeftShift => "Shift",
        KeyCode.Space => chinese ? "空格" : "Space", KeyCode.UpArrow => chinese ? "上箭头" : "Up", KeyCode.DownArrow => chinese ? "下箭头" : "Down",
        KeyCode.LeftArrow => chinese ? "左箭头" : "Left", KeyCode.RightArrow => chinese ? "右箭头" : "Right", _ => key.ToString()
    };
}

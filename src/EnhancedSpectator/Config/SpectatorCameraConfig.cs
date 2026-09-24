using BepInEx.Configuration;
using EnhancedSpectator.Features.Spectator;
using UnityEngine;

namespace EnhancedSpectator.Config;

/// <summary>Live local camera settings, independent of the fear-mode host gate.</summary>
public sealed class SpectatorCameraConfig
{
    /// <summary>Binds persistent distances, first-person FOV and cinematic controls.</summary>
    public SpectatorCameraConfig(ConfigFile file)
    {
        // Consume the old orphaned value before removing it, including stored false.
        // Backend selection is no longer a user setting.
        var retired = file.Bind("Diagnostics", "NativeFadePrototype", true, "Retired backend selector.");
        file.Remove(retired.Definition);
        ShowKeyHints = file.Bind("Spectator.UI", "ShowKeyHints", true, "Show spectator key hints. 显示观战按键提示。");
        ToggleKeyHintsKey = file.Bind("Spectator.UI", "ToggleKeyHintsKey", KeyCode.H, "Toggle spectator key hints. 切换观战按键提示。");
        ToggleHudKey = file.Bind("Spectator.UI", "ToggleHudKey", KeyCode.F2, "Hide/show spectator HUD for this life. 本次观战隐藏／显示全部界面。");
        SelfCameraDistanceModifier = file.Bind("Spectator.Camera", "SelfCameraDistanceModifier", KeyCode.LeftAlt,
            "Hold while scrolling in third person to adjust camera-to-model distance. 第三人称中按住此键滚轮调整镜头到自身模型的距离。");
        MoveForwardKey = file.Bind("Spectator.Freecam.Keys", "MoveForwardKey", KeyCode.W, "Move forward. 前进。");
        MoveBackKey = file.Bind("Spectator.Freecam.Keys", "MoveBackKey", KeyCode.S, "Move backward. 后退。");
        MoveLeftKey = file.Bind("Spectator.Freecam.Keys", "MoveLeftKey", KeyCode.A, "Move left. 向左。");
        MoveRightKey = file.Bind("Spectator.Freecam.Keys", "MoveRightKey", KeyCode.D, "Move right. 向右。");
        FreecamDistance = file.Bind("Spectator.Camera", "FreecamDistance", 4f,
            new ConfigDescription("Distance to the watched player; bounded by FreecamRadius. 自由视角距队友距离，受自由移动半径限制。", new AcceptableValueRange<float>(0.5f, 1000f)));
        FirstPersonFov = BindFov(file, "FirstPersonFov");
        FirstPersonKey = file.Bind("Spectator.Camera", "FirstPersonKey", KeyCode.F4, "Follow the watched player's eyes. 队友第一人称。");
        CinematicKey = file.Bind("Spectator.Camera", "CinematicKey", KeyCode.F5, "Toggle smooth cinematic tracking. 切换电影跟拍。");
        CinematicStyle = file.Bind("Spectator.Camera", "CinematicStyle", 0,
            new ConfigDescription("Cinematic style (0 = classic, 1–9 = new styles). Cycle with Left/Right in cinema. 电影风格，电影模式按左右方向键切换。", new AcceptableValueRange<int>(0, CinematicStyles.Count - 1)));
        CinematicDistance = file.Bind("Spectator.Camera", "CinematicDistance", 8f,
            new ConfigDescription("Orbit distance in metres. 电影跟拍距离（米）。", new AcceptableValueRange<float>(1.5f, SpectatorCameraRules.MaximumFollowDistance)));
        CinematicSpeed = file.Bind("Spectator.Camera", "CinematicSpeed", 16f,
            new ConfigDescription("Cinematic pacing; default 16 completes the continuous shot path in about 22.5 seconds. 运镜节奏，默认 16 对应约 22.5 秒的连续镜头循环。", new AcceptableValueRange<float>(0f, 30f)));
        GhostVoiceMuted = file.Bind("Spectator.Audio", "GhostVoiceMuted", false,
            "Mute locally routed ghost voices. 在本机静音鬼魂语音。");
        RetractAtWalls = file.Bind("Spectator.Camera", "RetractAtWalls", false,
            "Optional wall retraction in third person and cinema; off keeps enhanced-camera distance stable. 第三人称与电影遇墙收近，默认关闭以保持距离稳定。");
        SharedModelScale = file.Bind("FearMode", "SharedModelScale", 1f,
            new ConfigDescription("Your model size, relayed to compatible peers. 自己的模型大小，同步给兼容客户端。", new AcceptableValueRange<float>(0.1f, 5f)));
        FadeModelsNearby = file.Bind("FearMode", "FadeModelsNearby", true,
            "Smoothly fade models near the body of their watched teammate. 模型靠近被观战队友身体时平滑渐隐。");
        FadeRadius = file.Bind("FearMode", "FadeRadius", 2.5f,
            new ConfigDescription("Fade starts this far from the model envelope to the watched body. 模型外缘距离被观战队友身体小于此值时开始渐隐（米）。", new AcceptableValueRange<float>(0.5f, 10f)));
        FadeModelsWhileSpectating = file.Bind("FearMode", "FadeModelsWhileSpectating", false,
            "Also fade models when you are dead, including your own third-person model. 死亡观战时也渐隐模型（包括自身第三人称），默认关闭；不影响存活玩家的渐隐。");
        HearOtherFearSounds = file.Bind("Spectator.Audio", "HearOtherFearSounds", true,
            "Hear fear sounds played by other players; your own preview and voice are unaffected. 接收他人恐惧音效；不影响自己的音效试听与语音。");
        RepairPlayerNames = file.Bind("Spectator.Names", "RepairPlayerNames", true,
            "Preserve real name digits and remove only verified vanilla synthetic suffixes using Steam names. 参考 Steam 原名保留真实数字并修复原版添加的后缀。");
    }

    /// <summary>Optional collision retraction, disabled by default.</summary>
    public ConfigEntry<bool> RetractAtWalls { get; }
    /// <summary>Persistent default-on hint preference.</summary>
    public ConfigEntry<bool> ShowKeyHints { get; }
    /// <summary>Hint visibility shortcut.</summary>
    public ConfigEntry<KeyCode> ToggleKeyHintsKey { get; }
    /// <summary>Temporary whole-HUD visibility shortcut.</summary>
    public ConfigEntry<KeyCode> ToggleHudKey { get; }
    /// <summary>Modifier for camera-to-model zoom, independent of watched-player distance.</summary>
    public ConfigEntry<KeyCode> SelfCameraDistanceModifier { get; }
    /// <summary>Forward movement key.</summary>
    public ConfigEntry<KeyCode> MoveForwardKey { get; }
    /// <summary>Backward movement key.</summary>
    public ConfigEntry<KeyCode> MoveBackKey { get; }
    /// <summary>Left movement key.</summary>
    public ConfigEntry<KeyCode> MoveLeftKey { get; }
    /// <summary>Right movement key.</summary>
    public ConfigEntry<KeyCode> MoveRightKey { get; }
    /// <summary>Owner-selected scale sent through the fear selection protocol.</summary>
    public ConfigEntry<float> SharedModelScale { get; }
    /// <summary>Viewer-local distance fade.</summary>
    public ConfigEntry<bool> FadeModelsNearby { get; }
    /// <summary>Local viewer's fade-circle outer distance in metres.</summary>
    public ConfigEntry<float> FadeRadius { get; }
    /// <summary>Viewer-local opt-in for fading while dead, independent of host/client role.</summary>
    public ConfigEntry<bool> FadeModelsWhileSpectating { get; }
    /// <summary>Local opt-out of other players' fear sounds.</summary>
    public ConfigEntry<bool> HearOtherFearSounds { get; }
    /// <summary>Authoritative player-name repair, enabled by default.</summary>
    public ConfigEntry<bool> RepairPlayerNames { get; }

    /// <summary>Requested radial distance to the watched player.</summary>
    public ConfigEntry<float> FreecamDistance { get; }
    /// <summary>Watched-player eye camera FOV.</summary>
    public ConfigEntry<float> FirstPersonFov { get; }
    /// <summary>First-person shortcut.</summary>
    public ConfigEntry<KeyCode> FirstPersonKey { get; }
    /// <summary>Cinematic shortcut.</summary>
    public ConfigEntry<KeyCode> CinematicKey { get; }
    /// <summary>Persistent cinematic style; zero preserves the original sequence.</summary>
    public ConfigEntry<int> CinematicStyle { get; }
    /// <summary>Cinematic orbit distance.</summary>
    public ConfigEntry<float> CinematicDistance { get; }
    /// <summary>Cinematic orbit speed.</summary>
    public ConfigEntry<float> CinematicSpeed { get; }
    /// <summary>Persistent local ghost voice preference.</summary>
    public ConfigEntry<bool> GhostVoiceMuted { get; }

    private static ConfigEntry<float> BindFov(ConfigFile file, string key) => file.Bind(
        "Spectator.Camera", key, 66f,
        new ConfigDescription("First-person vertical FOV; adjustable in Options only. 第一人称垂直视野；仅通过选项调整。", new AcceptableValueRange<float>(30f, 110f)));
}

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
        ShowSpectatorRoster = file.Bind("Spectator.UI", "ShowSpectatorRoster", false,
            "Show the compact watch roster for alive players, up to 32 cards; off by default. 显示存活玩家的紧凑观战面板，最多 32 人；默认关闭，快捷键切换整个面板。");
        ToggleSpectatorRosterKey = file.Bind("Spectator.UI", "ToggleSpectatorRosterKey", KeyCode.O,
            "Toggle only the spectator watch panel. 单独显示／隐藏观战面板，可在按键设置中修改。");
        ToggleSpectatorCursorKey = file.Bind("Spectator.UI", "ToggleSpectatorCursorKey", KeyCode.P,
            "Release/restore the mouse to inspect viewer lists while spectating. 观战时显示／收起光标，悬停观众查看完整名单。");
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
        MonitorKey = file.Bind("Spectator.Camera", "MonitorKey", KeyCode.F1,
            "Camera choreography inside the facility only. 仅设施室内可用的运镜模式。");
        MonitorInfraredKey = file.Bind("Spectator.Camera", "MonitorInfraredKey", KeyCode.F9,
            "Toggle thermal imaging in all spectator views. 所有观战视角中开关红外热成像。");
        DebugMonitorCamera = file.Bind("Diagnostics", "DebugMonitorCamera", false,
            "Log monitor route state and blocking geometry once every two seconds while monitoring. Independent of global debug logging. 运镜专项诊断，每两秒记录路线与阻挡物，默认关闭。");
        BalanceSpectatorBrightness = file.Bind("Spectator.Image", "BalanceBrightness", true, "Lift spectator midtones locally without changing scene lights or living players. 本机观战中暗部提亮，不改变场景灯光或存活玩家。");
        MonitorInfrared = file.Bind("Spectator.Image", "MonitorInfrared", false,
            "Simulated color thermal imaging in all spectator views, off by default. 全部观战视角使用彩色热成像模拟，默认关闭，仅本机画面，不透墙。");
        SpectatorBrightness = file.Bind("Spectator.Image", "BrightnessStrength", 2f, new ConfigDescription("Bounded midtone lift, not automatic exposure or night vision. 观战亮度平衡强度，并非自动曝光或夜视。", new AcceptableValueRange<float>(0,2)));
        StabilizeFollow = file.Bind("Spectator.Camera", "StabilizeFollow", true, "Stabilize automatic target translation; preserves manual look and movement. 稳定跟随目标，保留手动转向和移动。");
        AutoCenter = file.Bind("Spectator.Camera", "AutoCenter", true, "Smoothly center the watched player after mouse inactivity. 鼠标闲置后平滑归位，使被观战队友居中。");
        HideAutoCenteringModels = file.Bind("Spectator.Models", "HideAutoCenteringModels", true, "While alive, hide other spectators who are automatically centering their camera. Local display preference only. 存活时隐藏正在自动归位的其他玩家观战模型，仅影响本机显示。");
        FollowSpeed = file.Bind("Spectator.Camera", "FollowSpeed", 1, new ConfigDescription("Follow response: 0 slow, 1 default, 2 fast. 镜头跟随：0 慢，1 默认，2 快。", new AcceptableValueRange<int>(0, 2)));
        TravellingCatchUpDistance = file.Bind("Spectator.Camera", "TravellingCatchUpDistance", 18f,
            new ConfigDescription("Travelling camera increases catch-up speed beyond this target distance and returns to normal pacing when close, in metres. 穿梭镜头超过此距离增加追赶速度，靠近后恢复；米。", new AcceptableValueRange<float>(6f, 60f)));
        HideAllModels = file.Bind("Spectator.Models", "HideAllModels", false, "Hide all spectator models locally, including default heads; audio and other players are unchanged. 本机隐藏全部观战模型（含默认鬼头），不影响音效和其他玩家。");
        ModelDisplayLimit = file.Bind("Spectator.Models", "DisplayLimit", 0, new ConfigDescription("Maximum visible spectator models on this client; nearest first, 0 unlimited. 本机同时显示的观战模型上限，优先近处，0 不限。", new AcceptableValueRange<int>(0, 64)));
        AutoMonitorIndoors = file.Bind("Spectator.Camera", "AutoMonitorIndoors", true,
            "Automatically use travelling indoors until a view is manually selected this life. 进入设施自动穿梭；本次死亡手动选择视角后不再自动接管。");
        MonitorStyle = file.Bind("Spectator.Camera", "MonitorStyle", 2,
            new ConfigDescription("Camera choreography style: 0 monitor, 2 rear travelling camera. Retired IDs 1 and 3 use monitor and travelling. 运镜风格：0 监视器，2 穿梭运镜；旧值 1、3 分别使用监视器与穿梭运镜；运镜模式内左右方向键切换。", new AcceptableValueRange<int>(0, MonitorCameraStyles.Count - 1)));
        CinematicStyle = file.Bind("Spectator.Camera", "CinematicStyle", 10,
            new ConfigDescription("Cinematic style (0 classic, 1–9 themed, 10 Shining follow, 11 centered follow). Cycle with Left/Right in cinema. 电影风格，电影模式按左右方向键切换。", new AcceptableValueRange<int>(0, CinematicStyles.Count - 1)));
        CinematicDistance = file.Bind("Spectator.Camera", "CinematicDistance", 8f,
            new ConfigDescription("Orbit distance in metres. 电影跟拍距离（米）。", new AcceptableValueRange<float>(1.5f, SpectatorCameraRules.MaximumFollowDistance)));
        CinematicSpeed = file.Bind("Spectator.Camera", "CinematicSpeed", 16f,
            new ConfigDescription("Cinematic pacing; action styles use 1.8x tempo. 运镜节奏；动作风格使用 1.8 倍节奏。", new AcceptableValueRange<float>(0f, 30f)));
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
        FadeModelsNearOtherPlayers = file.Bind("FearMode", "FadeModelsNearOtherPlayers", false,
            "Also fade models in other players' circles in your view, alive or dead. Master fade must be on; dead viewers also need FadeModelsWhileSpectating. 旁观他人透明圈时也渐隐，存活与死亡共用；需开启渐隐总开关，死亡时还需开启死亡观战渐隐。");
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
    /// <summary>Persistent watch-panel preference, disabled by default.</summary>
    public ConfigEntry<bool> ShowSpectatorRoster { get; }
    /// <summary>Persistent shortcut for the complete watch panel, independent of whole-HUD hiding.</summary>
    public ConfigEntry<KeyCode> ToggleSpectatorRosterKey { get; }
    /// <summary>Shortcut for transient viewer-list pointer mode.</summary>
    public ConfigEntry<KeyCode> ToggleSpectatorCursorKey { get; }
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
    /// <summary>One local opt-in for other players' circles, shared by living and dead viewers.</summary>
    public ConfigEntry<bool> FadeModelsNearOtherPlayers { get; }
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
    /// <summary>Indoor room-camera shortcut.</summary>
    public ConfigEntry<KeyCode> MonitorKey { get; }
    /// <summary>Thermal toggle shortcut for all spectator views.</summary>
    public ConfigEntry<KeyCode> MonitorInfraredKey { get; }
    /// <summary>Opt-in low-frequency monitor route diagnostics, independent of other logging.</summary>
    public ConfigEntry<bool> DebugMonitorCamera { get; }
    /// <summary>Automatically enter travelling until the viewer makes a manual selection.</summary>
    public ConfigEntry<bool> AutoMonitorIndoors { get; }
    /// <summary>Saved manually selected indoor camera style.</summary>
    public ConfigEntry<int> MonitorStyle { get; }
    /// <summary>Persistent cinematic style ID, independent of menu order.</summary>
    public ConfigEntry<int> CinematicStyle { get; }
    /// <summary>Cinematic orbit distance.</summary>
    public ConfigEntry<float> CinematicDistance { get; }
    /// <summary>Cinematic orbit speed.</summary>
    public ConfigEntry<float> CinematicSpeed { get; }
    /// <summary>Persistent local ghost voice preference.</summary>
    public ConfigEntry<bool> GhostVoiceMuted { get; }

    /// <summary>Local spectator presentation setting.</summary>
    public ConfigEntry<bool> BalanceSpectatorBrightness { get; }
    /// <summary>Optional simulated color thermal presentation for spectator views.</summary>
    public ConfigEntry<bool> MonitorInfrared { get; }
    /// <summary>Local spectator presentation setting.</summary>
    public ConfigEntry<float> SpectatorBrightness { get; }

    /// <summary>Default-on target-follow stabilization.</summary>
    public ConfigEntry<bool> StabilizeFollow { get; }

    /// <summary>Recenter enhanced views after manual look pauses.</summary>
    public ConfigEntry<bool> AutoCenter { get; }
    /// <summary>Living viewers may locally hide other spectators during their automatic centering.</summary>
    public ConfigEntry<bool> HideAutoCenteringModels { get; }
    /// <summary>Slow, default or fast camera response.</summary>
    public ConfigEntry<int> FollowSpeed { get; }
    /// <summary>Target distance in metres that activates additional travelling-camera catch-up speed.</summary>
    public ConfigEntry<float> TravellingCatchUpDistance { get; }
    /// <summary>Local master visibility for default and fear models.</summary>
    public ConfigEntry<bool> HideAllModels { get; }
    /// <summary>Visible model budget; zero means unlimited.</summary>
    public ConfigEntry<int> ModelDisplayLimit { get; }

    private static ConfigEntry<float> BindFov(ConfigFile file, string key) => file.Bind(
        "Spectator.Camera", key, 66f,
        new ConfigDescription("First-person vertical FOV; adjustable in Options only. 第一人称垂直视野；仅通过选项调整。", new AcceptableValueRange<float>(30f, 110f)));
}

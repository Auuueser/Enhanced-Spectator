using BepInEx.Configuration;
using UnityEngine;

namespace EnhancedSpectator.Config;

// Auto took the former Balanced slot and picks a quality, balanced or performance budget from the number of views.
internal enum SplitScreenQuality { Performance, Auto, Quality, Custom }

internal sealed class SplitScreenConfig
{
    internal readonly ConfigEntry<bool> AutoEnable, StartFocused, MainFollowsGame, ThermalAllViews, ShowSpeaking, ShowDeadBar, ShowViewerCount, StyledReport, ShowClock, ChatPopup;
    internal readonly ConfigEntry<KeyCode> ToggleKey, ClockKey, PreviewPanelKey;
    internal readonly ConfigEntry<SplitScreenQuality> Quality;
    internal readonly ConfigEntry<float> MainScale, TileScale;
    internal readonly ConfigEntry<int> MainFps, TileFps;

    internal SplitScreenConfig(ConfigFile file)
    {
        const string section = "Spectator.SplitScreen";
        AutoEnable = file.Bind(section, "AutoEnable", true, "Automatically show split-screen after death. 死亡进入观战后自动开启分屏。");
        ToggleKey = file.Bind(section, "ToggleKey", KeyCode.B, "Toggle spectator split-screen. 开关观战分屏。");
        StartFocused = file.Bind(section, "StartFocused", true, "Open with the watched player in a large view beside the small ones; off tiles every view. 开启时观察目标在大窗、其余在旁边小窗；关闭则全部平铺。");
        // "Preset" replaces the earlier "Quality" key, so existing profiles move to the adaptive default.
        Quality = file.Bind(section, "Preset", SplitScreenQuality.Auto, "Split-screen rendering preset; Auto picks one by the number of views. 分屏渲染档位；自适应按窗口数量选择。");
        MainFollowsGame = file.Bind(section, "MainFollowsGame", true, "Auto and Quality draw the large view at the game frame rate; Performance and Custom keep their limits. 自适应与质量档的大窗跟随游戏帧率；性能与自定义档不受影响。");
        ThermalAllViews = file.Bind(section, "ThermalAllViews", false, "When thermal imaging is on, also apply it to the small views and the tiled large view. 开启热成像时，小窗与平铺中的主窗也使用热成像。");
        ShowSpeaking = file.Bind(section, "ShowSpeaking", true, "Flash a view's name plate edge while its player speaks. 玩家说话时其窗口名牌边缘闪动提示。");
        ShowViewerCount = file.Bind(section, "ShowViewerCount", true,
            "Show how many are watching the large view under its name; hover it to see who. 在大窗名称下方显示正在观看的人数，光标移上去可查看是谁。");
        ShowDeadBar = file.Bind(section, "ShowDeadBar", true, "Show dead players with avatar and name under the views; their chip lights up while they speak. 在窗口下方显示阵亡玩家头像与名称，说话时高亮。");
        ShowClock = file.Bind(section, "ShowClock", false,
            "Show the game's clock at the top of the large view (tiled: the view you hear). 在大窗顶部（平铺时在正在收听的窗口）显示游戏时钟。");
        ClockKey = file.Bind(section, "ClockKey", KeyCode.T, "Show/hide the split-screen clock. 显示／隐藏分屏时钟。");
        ChatPopup = file.Bind(section, "ChatPopup", true,
            "Briefly show new chat messages (5 s) in the large view or a single spectator view; tiled, the chat shows only when you open it. The chat you open stays open until Esc or an empty line. 新聊天消息在大窗或单窗口观战中短暂弹出（5 秒）；平铺时只在你手动打开时显示。手动打开的聊天框保持打开，按 Esc 或发送空行关闭。");
        PreviewPanelKey = file.Bind(section, "PreviewPanelKey", KeyCode.K, "Show/hide the split-screen preview's control panel. 显示／隐藏分屏测试面板。");
        StyledReport = file.Bind(section, "StyledReport", true,
            "In split-screen, show the end-of-round report in split-screen style; off shows the game's own report (or another mod's) above the views. 分屏中以分屏风格显示回合结算；关闭则在分屏上方显示游戏自己（或其他模组）的结算界面。");
        MainScale = file.Bind(section, "MainScale", .9f, new ConfigDescription("Custom focused view resolution scale. 自定义大窗分辨率比例。", new AcceptableValueRange<float>(.25f, 1f)));
        TileScale = file.Bind(section, "TileScale", .8f, new ConfigDescription("Custom small view resolution scale. 自定义小窗分辨率比例。", new AcceptableValueRange<float>(.25f, 1f)));
        MainFps = file.Bind(section, "MainFps", 75, new ConfigDescription("Custom focused view FPS limit; 0 follows the game. 自定义大窗帧率上限；0 跟随游戏。", new AcceptableValueRange<int>(0, 120)));
        TileFps = file.Bind(section, "TileFps", 30, new ConfigDescription("Custom small view FPS limit; 0 follows the game. 自定义小窗帧率上限；0 跟随游戏。", new AcceptableValueRange<int>(0, 120)));
    }
}

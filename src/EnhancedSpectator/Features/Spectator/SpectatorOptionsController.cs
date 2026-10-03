using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Local options actions shared by the retained view and live config entries.</summary>
public sealed class SpectatorOptionsController
{
    private readonly EnhancedSpectatorConfig _config;
    internal SpectatorHotkeySettings Hotkeys { get; }
    /// <summary>Number of compact option rows.</summary>
    public const int RowCount = 30;
    /// <summary>Stable option rows in the three focused pages.</summary>
    public static int PageForRow(int row) => row == 29 ? 2 : row >= 27 ? 0 : row >= 24 ? 1 : row >= 22 ? 0 : row >= 20 ? 2 : row == 19 ? 0 : row <= 5 || row == 8 || row == 16 || row == 18 ? 0 : row <= 9 || row >= 14 ? 1 : 2;
    /// <summary>View and style cycle through choices with chevrons.</summary>
    public static bool IsSelector(int row) => row == 0 || row == 16 || row == 18 || row == 23;
    /// <summary>Whether this row uses a toggle rather than numeric step controls.</summary>
    public static bool IsToggle(int row) => (row >= 8 && row <= 14) || row == 17 || row == 20 || row == 19 || row == 22 || row == 24 || row == 26 || row == 27 || row == 28 || row == 29;
    /// <summary>Rows per sheet, reserving space for the model/audio header controls.</summary>
    public static int RowsPerSheet(int page) => page == 0 ? 8 : 6;
    /// <summary>Ordinal within a category, independent of stable row IDs.</summary>
    public static int Ordinal(int row) { int n=0; for(int i=0;i<row;i++) if(PageForRow(i)==PageForRow(row)) n++; return n; }
    /// <summary>Number of sheets in an option category.</summary>
    public static int SheetCount(int page) { int n=0; for(int i=0;i<RowCount;i++) if(PageForRow(i)==page) n++; return System.Math.Max(1,(n+RowsPerSheet(page)-1)/RowsPerSheet(page)); }
    /// <summary>Current value for a toggle row.</summary>
    public bool ToggleValue(int row) => row switch
    {
        29 => _config.Camera.ShowSpectatorRoster.Value,
        28 => _config.Camera.AutoMonitorIndoors.Value,
        27 => _config.Camera.MonitorInfrared.Value,
        26 => _config.Camera.HideAutoCenteringModels.Value,
        22 => _config.Camera.AutoCenter.Value,
        24 => _config.Camera.HideAllModels.Value,
        20 => _config.Camera.BalanceSpectatorBrightness.Value,
        19 => _config.Camera.StabilizeFollow.Value,
        8 => _config.Camera.RetractAtWalls.Value,
        9 => _config.Camera.FadeModelsNearby.Value,
        10 => _config.Camera.HearOtherFearSounds.Value,
        11 => _config.Camera.RepairPlayerNames.Value,
        12 => _config.Camera.ShowKeyHints.Value,
        13 => GameInterop.LethalCompanySpectatorUiVisibility.Requested,
        14 => _config.Camera.FadeModelsWhileSpectating.Value,
        17 => _config.Camera.FadeModelsNearOtherPlayers.Value,
        _ => false
    };
    /// <summary>Creates a live options controller.</summary>
    public SpectatorOptionsController(EnhancedSpectatorConfig config)
    { _config = config; Hotkeys = new SpectatorHotkeySettings(config); }

    private float DisplayedFreecamDistance => SpectatorFreecamController.Current is { OwnsCamera: true } controller
        && (controller.State.Mode == SpectatorCameraMode.Freecam || controller.State.Mode == SpectatorCameraMode.ThirdPerson)
            ? controller.State.Offset.magnitude : _config.Camera.FreecamDistance.Value;

    /// <summary>Explicit action for boolean options instead of ambiguous plus/minus buttons.</summary>
    public string ToggleActionLabel(int row, bool chinese)
    {
        bool enabled = ToggleValue(row);
        return chinese ? (enabled ? "关闭" : "开启") : (enabled ? "Turn off" : "Turn on");
    }

    /// <summary>Formats a row in the selected language.</summary>
    public string Describe(int row, bool chinese)
    {
        string[] labels = chinese
            ? ChineseLabels : EnglishLabels;
        if (row == 29) return (chinese ? "显示观战分布：" : "Show watch roster: ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off");
        if (row == 28) return (chinese ? "进入设施自动穿梭：" : "Auto travelling indoors: ") + (chinese ? ToggleValue(row)?"开启":"关闭" : ToggleValue(row)?"On":"Off");
        if (row == 27) return (chinese ? "红外热成像：" : "Thermal imaging: ") + (chinese ? ToggleValue(row)?"开启":"关闭" : ToggleValue(row)?"On":"Off");
        if (row == 26) return (chinese ? "存活时隐藏归位模型：" : "Hide centering models (alive): ") + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off");
        if (row == 22 || row == 24)
            return (row == 22 ? (chinese ? "闲置自动归位" : "Recenter when idle") : (chinese ? "隐藏所有观战模型" : "Hide all spectator models")) + ": "
                + (chinese ? ToggleValue(row) ? "开启" : "关闭" : ToggleValue(row) ? "On" : "Off");
        if (row == 23) return (chinese ? "镜头跟随：" : "Follow speed: ") + (chinese ? new[] { "慢", "默认", "快" } : new[] { "Slow", "Default", "Fast" })[_config.Camera.FollowSpeed.Value];
        if (row == 25) return (chinese ? "显示模型上限：" : "Visible model limit: ") + (_config.Camera.ModelDisplayLimit.Value == 0 ? (chinese ? "不限" : "Unlimited") : _config.Camera.ModelDisplayLimit.Value.ToString());
        if (row >= 19)
        {
            string label = row==19 ? (chinese ? "稳定跟随" : "Stable follow") : row==20 ? (chinese ? "平衡观战亮度" : "Balance spectator brightness") : (chinese ? "亮度平衡强度" : "Brightness strength");
            return label + ": " + (row==21 ? $"{_config.Camera.SpectatorBrightness.Value:0.##}" : chinese ? ToggleValue(row)?"开启":"关闭" : ToggleValue(row)?"On":"Off");
        }
        if (row == 18) return (chinese ? "运镜风格：" : "Camera style: ") + MonitorCameraStyles.Name(SpectatorFreecamController.Current?.EffectiveMonitorStyle ?? _config.Camera.MonitorStyle.Value, chinese);
        if (row == 16) return (chinese ? "电影风格：" : "Cinematic style: ") + CinematicStyles.Name(_config.Camera.CinematicStyle.Value, chinese);
        if (row == 17) return (chinese ? "旁观他人透明圈时渐隐: " : "Fade in others' circles: ")
            + (chinese ? (ToggleValue(row) ? "开启" : "关闭") : (ToggleValue(row) ? "On" : "Off"));
        if (row == 0)
        {
            var controller = SpectatorFreecamController.Current;
            int mode = controller?.OwnsCamera == true ? (int)controller.State.Mode + 1 : 0;
            return labels[row] + ": " + (chinese ? ChineseModes[mode] : EnglishModes[mode]);
        }
        if (IsToggle(row))
        {
            bool enabled = ToggleValue(row);
            return labels[row] + ": " + (chinese ? (enabled ? "开启" : "关闭") : (enabled ? "On" : "Off"));
        }
        float value = row switch
        {
            1 => _config.Camera.FirstPersonFov.Value,
            2 => DisplayedFreecamDistance,
            3 => _config.ThirdPersonDistance.Value,
            4 => _config.Camera.CinematicDistance.Value,
            5 => _config.Camera.CinematicSpeed.Value,
            7 => _config.Camera.SharedModelScale.Value,
            15 => _config.Camera.FadeRadius.Value,
            _ => _config.FearModelScaleMultiplier.Value
        };
        return labels[row] + $": {value:0.##}";
    }

    /// <summary>Applies one bounded UI step; values persist through their existing config entries.</summary>
    public void Adjust(int row, int direction)
    {
        if (row>=19)
        {
            if(row==29) _config.Camera.ShowSpectatorRoster.Value=!_config.Camera.ShowSpectatorRoster.Value;
            else if(row==28) _config.Camera.AutoMonitorIndoors.Value=!_config.Camera.AutoMonitorIndoors.Value;
            else if(row==27) _config.Camera.MonitorInfrared.Value=!_config.Camera.MonitorInfrared.Value;
            else if(row==26) _config.Camera.HideAutoCenteringModels.Value=!_config.Camera.HideAutoCenteringModels.Value;
            else if(row==22) _config.Camera.AutoCenter.Value=!_config.Camera.AutoCenter.Value;
            else if(row==23) _config.Camera.FollowSpeed.Value=(_config.Camera.FollowSpeed.Value+direction+3)%3;
            else if(row==24) _config.Camera.HideAllModels.Value=!_config.Camera.HideAllModels.Value;
            else if(row==25) _config.Camera.ModelDisplayLimit.Value=Mathf.Clamp(_config.Camera.ModelDisplayLimit.Value+direction,0,64);
            else if(row==19) _config.Camera.StabilizeFollow.Value=!_config.Camera.StabilizeFollow.Value;
            else if(row==20) _config.Camera.BalanceSpectatorBrightness.Value=!_config.Camera.BalanceSpectatorBrightness.Value;
            else if(row==21) _config.Camera.SpectatorBrightness.Value=Mathf.Clamp(_config.Camera.SpectatorBrightness.Value+direction*.1f,0,2);
            return;
        }
        if (row == 18)
        {
            var controller=SpectatorFreecamController.Current;
            int next=MonitorCameraStyles.Cycle(controller?.EffectiveMonitorStyle ?? _config.Camera.MonitorStyle.Value,direction);
            if(controller!=null) controller.SelectMonitorStyle(next);
            else _config.Camera.MonitorStyle.Value=next;
            return;
        }
        if (row == 16)
        {
            _config.Camera.CinematicStyle.Value = CinematicStyles.Cycle(_config.Camera.CinematicStyle.Value, direction);
            return;
        }
        if (row == 0)
        {
            var controller = SpectatorFreecamController.Current;
            if (controller == null) return;
            int current = controller.OwnsCamera ? (int)controller.State.Mode + 1 : 0;
            int next = (current + direction + ChineseModes.Length) % ChineseModes.Length;
            if (next == (int)SpectatorCameraMode.Monitor + 1 && !controller.CanUseMonitorView)
                next = (next + direction + ChineseModes.Length) % ChineseModes.Length;
            if (next == 0) controller.ReturnToVanilla();
            else controller.SelectMode((SpectatorCameraMode)(next - 1));
            return;
        }
        if (row == 1) _config.Camera.FirstPersonFov.Value = SpectatorCameraRules.AdjustFov(_config.Camera.FirstPersonFov.Value, -direction);
        else if (row == 2) _config.Camera.FreecamDistance.Value = SpectatorCameraRules.AdjustDistance(DisplayedFreecamDistance, -direction, 0.5f, _config.FreecamRadius.Value);
        else if (row == 3) _config.ThirdPersonDistance.Value = SpectatorCameraRules.AdjustDistance(_config.ThirdPersonDistance.Value, -direction, 1.5f, SpectatorCameraRules.MaximumFollowDistance);
        else if (row == 4) _config.Camera.CinematicDistance.Value = SpectatorCameraRules.AdjustDistance(_config.Camera.CinematicDistance.Value, -direction, 1.5f, SpectatorCameraRules.MaximumFollowDistance);
        else if (row == 5) _config.Camera.CinematicSpeed.Value = Mathf.Clamp(_config.Camera.CinematicSpeed.Value + direction, 0f, 30f);
        else if (row == 6) _config.FearModelScaleMultiplier.Value = Mathf.Clamp(_config.FearModelScaleMultiplier.Value + direction * 0.1f, 0.1f, 5f);
        else if (row == 7) _config.Camera.SharedModelScale.Value = Mathf.Clamp(_config.Camera.SharedModelScale.Value + direction * 0.1f, 0.1f, 5f);
        else if (row == 8) _config.Camera.RetractAtWalls.Value = !_config.Camera.RetractAtWalls.Value;
        else if (row == 9) _config.Camera.FadeModelsNearby.Value = !_config.Camera.FadeModelsNearby.Value;
        else if (row == 10) _config.Camera.HearOtherFearSounds.Value = !_config.Camera.HearOtherFearSounds.Value;
        else if (row == 11) _config.Camera.RepairPlayerNames.Value = !_config.Camera.RepairPlayerNames.Value;
        else if (row == 12) _config.Camera.ShowKeyHints.Value = !_config.Camera.ShowKeyHints.Value;
        else if (row == 15) _config.Camera.FadeRadius.Value = Mathf.Clamp(_config.Camera.FadeRadius.Value + direction * .25f, .5f, 10f);
        else if (row == 14) _config.Camera.FadeModelsWhileSpectating.Value = !_config.Camera.FadeModelsWhileSpectating.Value;
        else if (row == 17) _config.Camera.FadeModelsNearOtherPlayers.Value = !_config.Camera.FadeModelsNearOtherPlayers.Value;
        else if (row == 13) GameInterop.LethalCompanySpectatorUiVisibility.SetRequested(!GameInterop.LethalCompanySpectatorUiVisibility.Requested);
    }

    /// <summary>Restores only the visible options, preserving host authority and advanced settings.</summary>
    public void ResetDefaults(bool isHost)
    {
        SpectatorOptionsDefaults.Reset(_config, isHost);
        GameInterop.LethalCompanySpectatorUiVisibility.SetRequested(false);
        SpectatorFreecamController.Current?.ResetOptionsView();
    }

    private static readonly string[] ChineseLabels = { "观战视角", "第一人称 FOV", "观战距离（自由／第三人称）", "镜头到自身模型的距离", "电影跟拍距离", "电影运镜速度", "本机显示缩放", "我的模型大小（同步）", "遇墙自动收近", "靠近队友时渐隐", "接收他人恐惧音效", "玩家名称修复", "显示按键提示", "隐藏观战界面", "死亡观战时也渐隐模型", "渐隐范围（米）" };
    private static readonly string[] EnglishLabels = { "View", "First-person FOV", "Target distance (free / third)", "Camera-to-model distance", "Cinematic distance", "Cinematic speed", "Local display scale", "My model size (synced)", "Retract at walls", "Fade near watched player", "Hear others' fear sounds", "Repair player names", "Show key hints", "Hide spectator HUD", "Also fade while spectating", "Fade range (m)" };
    private static readonly string[] ChineseModes = { "原版", "增强自由视角", "自身第三人称", "队友第一人称", "电影跟拍", "运镜模式" };
    private static readonly string[] EnglishModes = { "Vanilla", "Freecam", "Self third person", "Player first person", "Cinematic", "Camera choreography" };
}

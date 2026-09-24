using EnhancedSpectator.Config;
using UnityEngine;

namespace EnhancedSpectator.Features.Spectator;

/// <summary>Local options actions shared by the retained view and live config entries.</summary>
public sealed class SpectatorOptionsController
{
    private readonly EnhancedSpectatorConfig _config;
    internal SpectatorHotkeySettings Hotkeys { get; }
    /// <summary>Number of compact option rows.</summary>
    public const int RowCount = 17;
    /// <summary>Stable option rows in the three focused pages.</summary>
    public static int PageForRow(int row) => row <= 5 || row == 8 || row == 16 ? 0 : row <= 9 || row >= 14 ? 1 : 2;
    /// <summary>View and style cycle through choices with chevrons.</summary>
    public static bool IsSelector(int row) => row == 0 || row == 16;
    /// <summary>Whether this row uses a toggle rather than numeric step controls.</summary>
    public static bool IsToggle(int row) => row >= 8 && row <= 14;
    /// <summary>Current value for a toggle row.</summary>
    public bool ToggleValue(int row) => row switch
    {
        8 => _config.Camera.RetractAtWalls.Value,
        9 => _config.Camera.FadeModelsNearby.Value,
        10 => _config.Camera.HearOtherFearSounds.Value,
        11 => _config.Camera.RepairPlayerNames.Value,
        12 => _config.Camera.ShowKeyHints.Value,
        13 => GameInterop.LethalCompanySpectatorUiVisibility.Requested,
        14 => _config.Camera.FadeModelsWhileSpectating.Value,
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
        if (row == 16) return (chinese ? "电影风格：" : "Cinematic style: ") + CinematicStyles.Name(_config.Camera.CinematicStyle.Value, chinese);
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
            int next = (current + direction + 5) % 5;
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
    private static readonly string[] ChineseModes = { "原版", "增强自由视角", "自身第三人称", "队友第一人称", "电影跟拍" };
    private static readonly string[] EnglishModes = { "Vanilla", "Freecam", "Self third person", "Player first person", "Cinematic" };
}

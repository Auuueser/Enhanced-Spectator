using System;
using System.Collections.Generic;
using System.Diagnostics;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using EnhancedSpectator.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnhancedSpectator.Features.SplitScreen;

internal sealed partial class SplitScreenModule : IFeatureModule, IRuntimeTickable, IRuntimeLateTickable
{
    internal static SplitScreenModule? Current { get; private set; }
    private readonly EnhancedSpectatorConfig _config;
    private readonly LethalCompanySplitScreenState _game = new();
    private readonly IGameSplitScreenRenderAdapter _renderer;
    private readonly SplitScreenRenderSchedule _schedule = new();
    private readonly SplitScreenRenderMetrics _metrics = new();
    private readonly SplitScreenTestPopulation _population = new();
    private readonly List<SplitScreenParticipant> _players = new(31), _sources = new(31);
    private readonly List<SplitScreenKey> _draws = new(31), _retired = new(31);
    private readonly HashSet<SplitScreenKey> _crossfade = new();
    private SplitScreenView? _view;
    private SplitScreenKey? _focused, _audio;
    private SpectatorCameraMode? _preferredMode = SpectatorCameraMode.Monitor;
    private int _preferredStyle = 2, _session, _previousCount;
    private bool _autoConsumed, _pointerRequested, _toggleRequested, _previewRequested, _exitPreviewRequested;
    private bool _previewHudVisible, _focusPending;
    private Vector2 _lastPointer;
    private readonly List<(string Label, string Keys)> _hintRows = new();
    private readonly SplitScreenWarmUp _warmUp = new();
    private bool _textWarmed;
    private readonly List<SplitScreenDeadPlayer> _dead = new(32);
    private readonly List<(SplitScreenKey Key, float Level)> _voices = new(64);
    private readonly List<string> _viewerNames = new(32);
    private static readonly List<SplitScreenDeadPlayer> NoDead = new();
    private float _nextVoice;
    // Watching together: the dead player followed, and the target last taken over from them.
    private SplitScreenKey? _watchWith, _watchTarget;
    // Watching together with a player who has a view enlarged: whose camera ours shows (client id), and whether it has.
    private ulong? _mirrorFrom;
    private bool _mirrored;
    // The camera this player had chosen before following took over theirs: given back when following ends.
    private (SpectatorCameraMode? Mode, int Style) _modeBeforeFollow;
    private readonly Func<ulong, ulong?> _remoteFollowing;
    private readonly HashSet<ulong> _followPath = new();
    private PreviewReturnState? _previewReturn;
    private float _nextDeath, _nextStatus;
    private string _statusText = string.Empty, _presetText = string.Empty;
    // All crew dead: one view, the ship taking off under the game's game-over camera, until the revive.
    internal static readonly SplitScreenKey ShipKey = new SplitScreenKey(ulong.MaxValue - 1, ulong.MaxValue - 1);
    private bool _takeoff;
    internal bool Takeoff => _takeoff;
    // The end-of-round report in split-screen style (the game shows its own a second after filling it).
    // The preview follows the game's order: music 0.75 s after the stats appear, the level about 9 s in, the
    // penalty about 3 s later, and 4 s after that the day passes: the days-left sound, as the report hands over.
    private const float PreviewMusicAt = .75f, PreviewLevelAt = 9, PreviewPenaltyAt = 12.3f, PreviewLevelDownAt = PreviewPenaltyAt + 1,
        PreviewDayAt = PreviewPenaltyAt + 4;
    private bool _previewMusic, _previewLevelUp, _previewLevelDrop, _previewLevelDown;
    private SplitScreenResultsPanel? _report;
    private readonly RoundReport _reportData = new();
    private readonly List<(string Name, bool Alive)> _reportPeople = new();
    private int _reportRound;
    private float _reportPreviewAt = -1;
    private bool _viewCovered;
    // Participation survives the moon unloading and the renderer stopping before the game's stats arrive.
    private bool _reportArmed;
    private bool _resume;
    // The last picture kept on screen (above the game's HUD) after the renderer let go as the ship left, until the
    // report covers it: the scene unload stops the views seconds before the game starts its report.
    private bool _frozen;
    // Frozen for the report (thawed when the report covers it), else for a resume (a scene loading as the ship lands);
    // a resume that has not come after this long gives the game's own view back.
    private bool _frozenForReport;
    private float _frozenAt;
    private const float ResumeFreezeSeconds = 15, RevivedSpectateUiSeconds = 2;

    internal bool Active { get; private set; }
    internal bool PresentsChat => Active && _view?.ChatAvailable == true;
    internal bool Preview { get; private set; }
    internal Canvas? OverlayCanvas => Active && !Preview ? _view?.HudCanvas : null;
    internal int ParticipantCount => _players.Count;
    internal SpectatorCameraMode? SelectedMode => _focused.HasValue ? _renderer.PrimaryMode : null;
    private bool _hintsStale;
    private bool _voteHint, _voteController;
    private readonly List<string> _chatHistory = new();
    private int _chatSeen = -1;
    private void RebuildHints()
    {
        _hintRows.Clear();
        foreach (var row in SpectatorHintText.Rows(_config, null, false)) _hintRows.Add((row.Label, row.Keys));
        if (Preview)
        {
            bool cn = _config.UseChineseText;
            _hintRows.Add((_previewPanelHidden ? cn ? "显示测试面板" : "Show the preview panel" : cn ? "隐藏测试面板" : "Hide the preview panel",
                SpectatorHotkeySettings.KeyLabel(_config.Camera.SplitScreen.PreviewPanelKey.Value, cn)));
        }
        // The vote goes right under the status line ("Now: ..."), ahead of every key.
        _voteHintRow = !_voteHint ? -1 : _hintRows.Count > 0 && _hintRows[0].Keys.Length == 0 ? 1 : 0;
        bool chinese = _config.UseChineseText;
        if (_voteHintRow >= 0)
            _hintRows.Insert(_voteHintRow, (chinese ? "投票：飞船提前离开" : "Vote: ship leaves early",
                _voteController ? chinese ? "按住 RT" : "Hold RT" : chinese ? "按住右键" : "Hold RMB"));
    }
    private int _voteHintRow = -1;
    /// <summary>The choreography camera works for the view a mode key applies to (the enlarged one, else the audio one): indoors only.</summary>
    internal bool MonitorAvailable => Find(_focused ?? _audio) is { } target && _renderer.IsIndoors(target);
    /// <summary>The enlarged view's camera as drawn: a choreography selection outdoors shows the vanilla view.</summary>
    internal SpectatorCameraMode? EffectiveMode => SelectedMode == SpectatorCameraMode.Monitor && !MonitorAvailable ? null : SelectedMode;
    /// <summary>This player's split-screen state as published to others (a preview is local only).</summary>
    internal static Networking.SpectatorSplitView LocalView => Current is { Active: true, Preview: false } module
        ? module.LargeViewOpen ? Networking.SpectatorSplitView.Watching : Networking.SpectatorSplitView.Audience
        : Networking.SpectatorSplitView.None;
    /// <summary>A view is enlarged (whatever its camera, vanilla included).</summary>
    internal bool LargeViewOpen => _focused.HasValue;
    internal void SelectViewMode(SpectatorCameraMode? mode) => SelectMode(mode);
    internal void SelectViewStyle(int style)
    {
        _preferredStyle = style;
        _config.Camera.MonitorStyle.Value = style;
        if (_focused.HasValue) SelectMode(_preferredMode, style);
    }
    internal string ResolutionText(bool main, float scale)
    {
        var key = main ? _focused : null;
        if (!main)
            foreach (var player in _players)
                if (player.Key != _focused) { key = player.Key; break; }
        if (key.HasValue && _view != null && _view.TryGetTargetSize(key.Value, out var size))
        {
            SplitScreenRenderBudget.Size(size.y, scale, out int width, out int height);
            return $"{width}×{height}";
        }
        bool focusedLayout = main || _focused.HasValue;
        var rects = SplitScreenLayout.Calculate(Math.Max(1, _players.Count), Screen.width, Screen.height, focusedLayout ? 0 : -1,
            Preview ? SplitScreenTestToolbar.ReservedHeight(new Vector2(Screen.width, Screen.height)) : 0);
        int index = !main && focusedLayout && rects.Length > 1 ? 1 : 0;
        SplitScreenRenderBudget.Size(rects[index].Height, scale, out int w, out int h);
        return $"{w}×{h}";
    }
    /// <summary>Who a dead player (by client id) is watching, from the synced spectator targets.</summary>
    internal Func<ulong, SplitScreenKey?>? RemoteTarget { get; set; }
    /// <summary>A mod peer's split-screen state and the audience member (client id) they follow, from their pose.</summary>
    internal Func<ulong, (Networking.SpectatorSplitView View, ulong? Following)>? RemoteSplit { get; set; }
    /// <summary>
    /// A mod peer's camera as presented here (their pose resolved locally), the kind of camera it is (null for the
    /// game's own view) with its choreography style, and the view it shows.
    /// </summary>
    internal Func<ulong, (Vector3 Position, Quaternion Rotation, SpectatorCameraMode? Mode, int Style, SplitScreenKey? Target, float FieldOfView)?>? RemoteCamera { get; set; }
    /// <summary>Whether a client runs Enhanced Spectator (completed handshake).</summary>
    internal Func<ulong, bool>? IsModPeer { get; set; }
    /// <summary>Another overlay above the views (the emote picker) that owns clicks at this point.</summary>
    internal Func<Vector2, bool>? OwnsPointer { get; set; }
    /// <summary>Screen height the audience row occupies at the bottom.</summary>
    internal float AudienceHeight => Active && _view != null ? _view.DeadBarHeight : 0;
    internal void ShowEmote(ulong sender, string text) { if (Active) _view?.ShowEmote(sender, text); }
    /// <summary>Social preview buttons on the test toolbar; the social module runs them locally.</summary>
    internal Action<SplitScreenTestAction>? SocialTest;
    /// <summary>Preview identities (slot, name, alive) for the local social preview.</summary>
    internal void CopyPreviewPeopleTo(List<(int Slot, string Name, bool Alive)> output)
    { _population.Chinese = _config.UseChineseText; _population.CopyPeopleTo(_sources, output); }
    /// <summary>Screen height the preview toolbar takes at the top; overlays start below it.</summary>
    internal float TopReserved => Active && Preview && !DemoRunning ? SplitScreenTestToolbar.ReservedHeight(new Vector2(Screen.width, Screen.height)) : 0;
    /// <summary>The preview's seat for the local viewer at the end of the audience row.</summary>
    internal const int PreviewLocalSlot = 900;
    /// <summary>An emote above a preview audience member.</summary>
    internal void ShowPreviewEmote(int slot, string text) { if (Preview) _view?.ShowEmote(new SplitScreenKey(ulong.MaxValue, (ulong)slot), text); }
    private bool _previewSpeaking, _previewSuspended, _previewPanelHidden;
    // The preview's control panel: hidden by its key, and stepping aside while a demonstration plays.
    private void ShowPreviewPanel()
    {
        _view?.SetTestVisible(!_previewPanelHidden && _votePreviewAt < 0 && !DemoRunning, OnTestAction);
        _view?.SetTestPanelKey(SpectatorHotkeySettings.KeyLabel(_config.Camera.SplitScreen.PreviewPanelKey.Value, _config.UseChineseText));
        RebuildHints();
    }
    internal SplitScreenModule(EnhancedSpectatorConfig config)
    { _config = config; _renderer = new LethalCompanySplitScreenRenderAdapter(config); _remoteFollowing = id => RemoteSplit?.Invoke(id).Following; }
    public void Initialize() { Current = this; }
    internal void RequestToggle() => _toggleRequested = true;
    internal void RequestPreview() => _previewRequested = true;
    internal bool OwnsVideoCanvas(Canvas canvas) => (Active || _frozen) && _view?.OwnsVideoCanvas(canvas) == true
        || _report?.Visible == true && canvas == _report.Canvas;
    /// <summary>Any canvas the split-screen draws (views, labels, audience row, test toolbar, report).</summary>
    internal bool OwnsCanvas(Canvas canvas) => (Active || _frozen) && _view?.OwnsCanvas(canvas) == true || _report?.Visible == true && canvas == _report.Canvas;

    public void Tick()
    {
        var context = _game.Read();
        if (_session != context.Session)
        { HideReport(); FinishPreview(false); Stop(); LethalCompanyRoundResults.Clear(); _session = context.Session; _autoConsumed = _resume = false; }
        // Revival can occur while the scene-transition guard still blocks the rest of this tick.
        if (!context.Dead) _autoConsumed = _resume = false;
        // Built ahead (hidden) once in a game, so the first death only starts it: building its canvases, texts and
        // textures at that moment was a noticeable hitch the first time.
        if (_view == null && context.Session != 0 && _config.EnableEnhancedSpectator.Value && LethalCompanySplitScreenState.Font != null) EnsureView();
        if (!_config.EnableEnhancedSpectator.Value)
        { _resume = false; HideReport(); FinishPreview(false); Stop(); return; }
        if (Active && !Preview && context.ShipLeaving && _config.Camera.SplitScreen.StyledReport.Value) _reportArmed = true;
        if (_frozen && (!context.Dead || (_frozenForReport ? !context.ShipLeaving : !_resume || Time.unscaledTime - _frozenAt > ResumeFreezeSeconds))) Thaw();
        if (!RuntimeConnectionState.CanRunLocalDiagnostics(out _))
        { bool freeze = Freezes(context); if (Active && !Preview) Interrupt(context); FinishPreview(false); Stop(freeze); return; }
        // Alive in an enabled, usable session: compile the first opening's code, then prepare its hint glyphs.
        if (_view != null && !context.Dead && !Active)
        {
            if (!_warmUp.Done) _warmUp.Step();
            else if (!_textWarmed) { _textWarmed = true; RebuildHints(); _view.WarmText(_hintRows); }
        }
        if (_exitPreviewRequested)
        {
            _exitPreviewRequested = false;
            FinishPreview(context.Eligible);
        }
        if (_previewRequested)
        {
            _previewRequested = false;
            if (!Preview && context.Session != 0 && context.MenuOpen)
            {
                _game.CopyLivingPlayersTo(_sources, true);
                if (_sources.Count > 0)
                {
                    _previewReturn = new PreviewReturnState(this);
                    Stop(); _population.SetCount(4);
                    if (!Start(true)) FinishPreview(context.Eligible);
                }
            }
        }
        if (Preview)
        {
            // Only the toolbar's Exit action ends a preview (besides the session and runtime guards above).
            // Closing ESC hands the keys back to the real player; the cursor and toolbar return with the menu.
            if (context.PreviewInputBlocked) { _view?.SetHover(null); return; }
            if (_reportPreviewAt >= 0)
            {
                if (Time.unscaledTime - _reportPreviewAt > .3f && Mouse.current?.leftButton.wasPressedThisFrame == true) _report!.Close();
                return;
            }
            if (context.MenuOpen) HandlePointer(); else if (!DemoRunning) { _view?.SetHover(null); _view?.SetViewersHover(false); }
            if (TryReadModeKey(out var mode)) SelectMode(mode);
            if (SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.ToggleHudKey.Value)) _previewHudVisible = !_previewHudVisible;
            if (SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.SplitScreen.PreviewPanelKey.Value))
            { _previewPanelHidden = !_previewPanelHidden; ShowPreviewPanel(); }
            HandleClockKey();
            // The split-screen and pointer keys act as they do while spectating: the views close and reopen (the
            // toolbar stays), and the pointer switches between the preview (ESC menu) and the character.
            if (SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.SplitScreen.ToggleKey.Value))
            { _previewSuspended = !_previewSuspended; _view?.SetSuspended(_previewSuspended); }
            if (SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.ToggleSpectatorCursorKey.Value)) _game.TogglePreviewPointer();
            HandleThermalKeys();
            return;
        }
        if (!context.Dead && Active) Stop();
        if (Active && !context.Eligible) { bool freeze = Freezes(context); Interrupt(context); Stop(freeze); }
        bool toggle = _toggleRequested || (!context.InputBlocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.SplitScreen.ToggleKey.Value));
        _toggleRequested = false;
        if (toggle && (context.Eligible || _frozen))
        { _autoConsumed = true; _resume = false; if (Active || _frozen) { HideReport(); Stop(); } else Start(false); }
        else if (!Active && context.Eligible && (_resume || !_autoConsumed && _config.Camera.SplitScreen.AutoEnable.Value) && Start(false))
            { _autoConsumed = true; _resume = false; }
        if (!Active) return;
        bool blocked = context.InputBlocked;
        if (!blocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.ToggleSpectatorCursorKey.Value)) _pointerRequested = !_pointerRequested;
        LethalCompanySpectatorRosterCursor.Shared.Update(true, blocked,
            !blocked && SpectatorPointerCapture.IsActive != _pointerRequested, false);
        if (!blocked)
        {
            // With the pointer handed to camera control the cursor is hidden at the screen centre. A click then
            // returns the cursor and the grid, rather than vanilla's click-to-next-target changing the large view.
            if (!SpectatorPointerCapture.IsActive && Mouse.current?.leftButton.wasPressedThisFrame == true)
            { _pointerRequested = true; if (_focused.HasValue) Focus(null); }
            else HandlePointer();
            HandleClockKey();
            if (!_focused.HasValue && !_takeoff)
            {
                // While following, the camera is the followed player's: the keys are ignored (see FollowWatchTogether).
                if (!_watchWith.HasValue && TryReadModeKey(out var mode)) SelectMode(mode);
                // The camera controller does not consume keys while tiled.
                HandleThermalKeys();
            }
        }
    }

    // Closed by the game rather than the player while still dead (a scene loading as the ship lands, a camera hand-off):
    // it opens again once spectating resumes. The ship leaving ends the round instead, through the takeoff and report.
    private void Interrupt(SplitScreenSessionContext context) => _resume = context.Dead && !context.ShipLeaving;

    private void HandleClockKey()
    {
        var split = _config.Camera.SplitScreen;
        if (SpectatorInputService.IsKeyPressedThisFrame(split.ClockKey.Value)) split.ShowClock.Value = !split.ShowClock.Value;
    }

    private void HandleThermalKeys()
    {
        // The camera controller ticks after this module: a key handled here must only take effect once.
        if (SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.MonitorInfraredKey.Value))
        { _config.Camera.MonitorInfrared.Value = !_config.Camera.MonitorInfrared.Value; SpectatorFreecamController.Current?.SuppressModeInputThisFrame(); }
        if (_config.Camera.MonitorInfrared.Value && (SpectatorInputService.IsKeyPressedThisFrame(KeyCode.UpArrow) || SpectatorInputService.IsKeyPressedThisFrame(KeyCode.DownArrow)))
        { SpectatorFreecamController.CycleThermalPalette(_config.Camera); SpectatorFreecamController.Current?.SuppressModeInputThisFrame(); }
    }

    private bool Start(bool preview)
    {
        if (!_renderer.TryBegin(preview)) return false;
        Thaw();
        Active = true; Preview = preview; _focused = null; _audio = null; _previewSuspended = _previewPanelHidden = false; _takeoff = false; _viewCovered = false;
        _focusPending = _config.Camera.SplitScreen.StartFocused.Value; _previousCount = 0; _nextVoice = 0;
        SpectatorVanillaInputGuard.SplitScreenActive = !preview;
        _previewHudVisible = true;
        _pointerRequested = !preview;
        LethalCompanySpectatorRosterCursor.SplitScreenOwnsInput = !preview;
        _metrics.Reset(Time.unscaledTimeAsDouble); _nextStatus = 0; _statusText = string.Empty;
        EnsureView(); _view!.SetTestVisible(preview, OnTestAction); _view.SetVisible(true); _view.WakeKeyHints();
        _view.SetTestPanelKey(SpectatorHotkeySettings.KeyLabel(_config.Camera.SplitScreen.PreviewPanelKey.Value, _config.UseChineseText));
        _schedule.Clear(); _crossfade.Clear();
        ModLog.Debug(preview ? "Split-screen local preview opened." : "Spectator split-screen opened.");
        return true;
    }

    private void EnsureView()
    {
        if (_view == null || !_view.IsReady)
        {
            _view?.Dispose(); _view = new SplitScreenView();
            _view.ConfigureFont(LethalCompanySplitScreenState.Font);
        }
        _view.SetSize(new Vector2(Screen.width, Screen.height));
    }

    public void LateTick()
    {
        long started = Stopwatch.GetTimestamp();
        ReportSpike();
        var context = _game.Read();
        LethalCompanyChat.PreviewPresents = false;
        TickReport(context);
        TickViews(context);
        if (_frozen)
        {
            _view!.SetBeneath(context.GameUiOpen ? LethalCompanySplitScreenState.HudCanvas : null);
            LethalCompanySplitScreenUiPresentation.MenuOver = context.GameUiOpen && !LethalCompanySplitScreenUiPresentation.ReportOwned;
        }
        // Use the final canvas state, including any renderer stop or early return during this update.
        // The report can keep drawing beneath the HUD after the view renderer has released its resources.
        _game.HudCanvasBeneath((Active || _frozen) && _view?.DrawsBeneathHud == true || _report?.DrawsBeneathHud == true);
        _spikeUpdateMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
    }

    // A frame far over budget soon after the layout changed (a view enlarged, another one, or back to tiles): with
    // diagnostics on, one log line saying where its time went, so a first-switch hitch can be traced in the game. The slowest view's render
    // submission includes HDRP's work for that camera (a first render at a new size allocates its buffers); the rest
    // of the frame is outside the split-screen.
    private const float SpikeSeconds = .08f, SpikeWindow = 2;
    private float _layoutChangedAt = float.NegativeInfinity, _nextSpikeLog;
    private double _spikeUpdateMs, _spikeRenderMs, _spikeTotalRenderMs;
    private double _spikeUiMs, _spikePoseMs, _spikeSceneMs, _spikeCleanupMs;
    private int _spikeWidth, _spikeHeight;
    private bool _spikeNewSize;
    private void ReportSpike()
    {
        float frame = Time.unscaledDeltaTime, since = Time.unscaledTime - _layoutChangedAt;
        if (ModLog.IsDebugEnabled && Active && frame > SpikeSeconds && since < SpikeWindow && Time.unscaledTime >= _nextSpikeLog)
        {
            _nextSpikeLog = Time.unscaledTime + 1;
            ModLog.Debug($"Split-screen frame spike: {frame * 1000:F0} ms, {since:F2} s after a layout change ({(_focused.HasValue ? "large view" : "tiled")}, "
                + $"{_players.Count} views, {_renderer.CameraCount} cameras). Split-screen update {_spikeUpdateMs:F1} ms; "
                + $"all view submissions {_spikeTotalRenderMs:F1} ms; other split work {Math.Max(0, _spikeUpdateMs - _spikeTotalRenderMs):F1} ms; slowest view render "
                + $"{_spikeRenderMs:F1} ms at {_spikeWidth}x{_spikeHeight}{(_spikeNewSize ? " (first render at this size)" : "")}; "
                + $"view preparation {_spikeUiMs:F1} ms; primary pose {_spikePoseMs:F1} ms; scene preparation {_spikeSceneMs:F1} ms; "
                + $"texture cleanup {_spikeCleanupMs:F1} ms; remaining split work "
                + $"{Math.Max(0, _spikeUpdateMs - _spikeTotalRenderMs - _spikeUiMs - _spikePoseMs - _spikeSceneMs - _spikeCleanupMs):F1} ms.");
        }
        _spikeRenderMs = _spikeTotalRenderMs = 0; _spikeNewSize = false;
        _spikeUiMs = _spikePoseMs = _spikeSceneMs = _spikeCleanupMs = 0;
    }

    private void TickViews(SplitScreenSessionContext context)
    {
        long phaseStarted = Stopwatch.GetTimestamp();
        if (!Active) return;
        if (Preview && _previewSuspended)
        {
            // Closed with the split-screen key: nothing renders; the toolbar says how to reopen.
            _view!.UpdateStatus(_config.UseChineseText ? "分屏画面已关闭 · 再按 " + SpectatorHotkeySettings.KeyLabel(_config.Camera.SplitScreen.ToggleKey.Value, true) + " 打开"
                : "Split-screen closed · press " + SpectatorHotkeySettings.KeyLabel(_config.Camera.SplitScreen.ToggleKey.Value) + " to reopen",
                _config.UseChineseText, _players.Count, _population.Playing, _presetText, _focused ?? _audio,
                PreviewModeIndex(), _previewHudVisible, _config.Camera.MonitorInfrared.Value, _previewSpeaking, _population.Audience, _demoCaptions);
            return;
        }
        if (DemoRunning) TickDemo(context);
        if (Preview)
        {
            _game.CopyLivingPlayersTo(_sources, true);
            if (_population.Playing && Time.unscaledTime >= _nextDeath)
            {
                _population.KillRandom(); _nextDeath = Time.unscaledTime + 1f;
                if (_population.Count == 0) _population.Playing = false;
            }
            _population.CopyTo(_sources, _players);
            if (!_audio.HasValue || !Contains(_audio.Value)) _audio = _players.Count > 0 ? _players[0].Key : null;
        }
        else
        {
            _game.CopyLivingPlayersTo(_players, false);
            if (!context.Eligible) { bool freeze = Freezes(context); Interrupt(context); Stop(freeze); return; }
            if (_players.Count == 0 && !context.AllDead && !context.GameOverCamera) { Stop(); return; }
            // The ship taking off, seen from outside as the game shows it: with the whole crew dead, and also when it
            // leaves early (vote) or at midnight with living crew aboard. One view of it, as for the dead crew.
            bool takeoff = _players.Count == 0 || context.GameOverCamera;
            // Unheard first: the large view's last player must not pose the game's camera on the way out.
            if (takeoff && !_takeoff) { StopWatchTogether(ownCamera: false); _audio = null; Focus(null); }
            _takeoff = takeoff;
            if (takeoff) { _players.Clear(); _players.Add(new SplitScreenParticipant(ShipKey, ShipKey, _config.UseChineseText ? "飞船起飞" : "Ship taking off")); }
            _audio = takeoff ? null : context.Target;
        }
        // The split-screen style report covers the screen: nothing beneath it needs drawing.
        bool covered = _report != null && _report.Covering;
        if (covered != _viewCovered) { _viewCovered = covered; _view?.SetVisible(!covered); }
        // The current report's actual owner controls presentation. A report that started as native stays above the
        // views even when the style setting is on, or when the split-screen was opened part-way through it.
        bool gameReport = LethalCompanyRoundResults.Open && !LethalCompanySplitScreenUiPresentation.ReportOwned;
        // The ship view draws beneath the HUD so the game's dialogue and game-over screen stay over it; everything
        // else there (its clock, other mods' panels) is hidden while no menu or chat is open, and the clock is ours.
        // The game's own report (style setting off) stays above it; ours covering the view isolates itself.
        // A menu (or another mod's text field) draws over the views from beneath; the chat line is typed in the
        // split-screen's own chat panel, the views staying on top.
        bool overlay = context.MenuOpen || context.GameUiOpen && !context.Chat;
        LethalCompanySplitScreenUiPresentation.Takeoff = _takeoff && !covered && !overlay && !gameReport;
        // With a menu or the chat open the views draw beneath the game HUD: of it only the menu and chat (and the
        // dialogue and game-over texts) draw then, not its clock, spectator UI or other mods' boxes there.
        LethalCompanySplitScreenUiPresentation.MenuOver = !Preview && !covered && overlay && !gameReport;
        if (covered) return;
        if (_focused.HasValue && !Contains(_focused.Value)) _focused = null;
        // The default layout puts the watched player in the large view beside the others.
        if (_focusPending && _audio.HasValue && Contains(_audio.Value)) { _focusPending = false; Focus(_audio); }
        // The last player left fills the screen with the travelling camera.
        if (_players.Count == 1 && _previousCount != 1 && !_takeoff)
        { _focusPending = false; Focus(_players[0].Key); _preferredMode = SpectatorCameraMode.Monitor; SelectViewStyle(2); }
        _previousCount = _players.Count;
        // Wheel/roster target changes keep the focused view and its audio together.
        // A dead focused player has already returned the layout to the grid above.
        if (!Preview && _focused.HasValue && _audio.HasValue && _focused != _audio && Contains(_audio.Value)) Focus(_audio);
        EnsureView();
        // Views stay live through ESC, chat and window focus loss. A menu or the chat draws over them.
        // The game's clock (when switched on) sits in the large view, else in the view being heard (the ship view at
        // takeoff), while the day runs on a moon. The game's own report (style setting off) is left alone.
        bool day = !gameReport && LethalCompanySplitScreenUiPresentation.ClockRunning;
        var clockView = !day || !_config.Camera.SplitScreen.ShowClock.Value ? null
            : _takeoff ? ShipKey : _focused ?? _audio ?? (_players.Count > 0 ? _players[0].Key : (SplitScreenKey?)null);
        _view!.SetClock(clockView, clockView.HasValue ? LethalCompanySplitScreenUiPresentation.ClockText : null,
            clockView.HasValue ? LethalCompanySplitScreenUiPresentation.ClockIcon : null, clockView.HasValue ? LethalCompanySplitScreenUiPresentation.ClockPhase : -1);
        // The early-leave vote strip above the views; until this player votes the key hints lead with the vote.
        var vote = Preview ? PreviewLeaveVote() : day && !_takeoff ? LethalCompanyLeaveVote.Read() : default;
        _view.SetLeaveVote(vote, _config.UseChineseText);
        // The chat panel in the large (or ship) view; tiled, it opens beside the views while the line is typed.
        _chatSeen = LethalCompanyChat.Read(_chatHistory, _chatSeen);
        bool ownChat = Preview || !overlay && !gameReport;
        LethalCompanyChat.PreviewPresents = Preview && _view.ChatAvailable;
        _view.MatchChatText(LethalCompanyChat.MessagesText, LethalCompanyChat.LineText);
        _view.SetChat(_takeoff ? ShipKey : _focused, context.Chat && ownChat, context.Chat ? LethalCompanyChat.Input : string.Empty, _chatHistory, _chatSeen,
            ownChat && _config.Camera.SplitScreen.ChatPopup.Value, _config.UseChineseText,
            context.Chat ? LethalCompanyChat.Caret : -1, context.Chat ? LethalCompanyChat.Composing : string.Empty);
        bool voteHint = vote.Prompt && !vote.Voted;
        if (voteHint != _voteHint || vote.Controller != _voteController) { _voteHint = voteHint; _voteController = vote.Controller; RebuildHints(); }
        // Our report yielding to the game's menu or chat goes beneath the same HUD: the PlayerScreen must not cover it.
        bool reportBeneath = LethalCompanySplitScreenUiPresentation.ReportOwned && context.GameUiOpen;
        _view!.SetBeneath(!Preview && (overlay || _takeoff || gameReport || reportBeneath) ? LethalCompanySplitScreenState.HudCanvas : null);
        _view.SetHudVisible(Preview ? _previewHudVisible : !LethalCompanySpectatorUiVisibility.Hidden);
        // Voices and the dead-player bar refresh ten times a second, before the layout reserves the bar's space.
        if (Time.unscaledTime >= _nextVoice)
        {
            _nextVoice = Time.unscaledTime + .1f;
            var split = _config.Camera.SplitScreen;
            // The audience is read even with its row hidden: the large view's viewer count comes from it.
            if (Preview)
            {
                _population.Chinese = _config.UseChineseText;
                _population.CopyDeadTo(_sources, _dead, _game.SteamIdOf);
                if (_sources.Count > 0)
                    _dead.Add(new SplitScreenDeadPlayer(new SplitScreenKey(ulong.MaxValue, PreviewLocalSlot), _sources[0].Source,
                        _config.UseChineseText ? "测试玩家" : "Preview player", 0, true));
            }
            else _game.CopyDeadPlayersTo(_dead);
            // Each audience member's split state and current target: synced for mod peers, our own for us; preview
            // identities watch a preview view so watching together can be tried alone. With every view tiled a
            // spectator still watches (the orange audio view) from the audience, without standing by that player.
            for (int i = 0; i < _dead.Count; i++)
            {
                var entry = _dead[i];
                // Every fourth preview audience member stands in for a player without the mod.
                bool enhanced = Preview ? entry.Key.SlotId % 4 != 3 : entry.Local || IsModPeer?.Invoke(entry.Source.ClientId) == true;
                (Networking.SpectatorSplitView View, ulong? Following) member = !enhanced ? (Networking.SpectatorSplitView.None, (ulong?)null)
                    : entry.Local ? (_focused.HasValue ? Networking.SpectatorSplitView.Watching : Networking.SpectatorSplitView.Audience, LocalFollowingId())
                    : Preview ? (Networking.SpectatorSplitView.Watching, (ulong?)null)
                    : RemoteSplit?.Invoke(entry.Source.ClientId) ?? (Networking.SpectatorSplitView.None, (ulong?)null);
                SplitScreenKey? watching = !enhanced ? null
                    : entry.Local ? (Preview ? _audio : context.Target)
                    : Preview ? (_players.Count > 0 ? _players[(int)(entry.Key.SlotId % (ulong)_players.Count)].Key : null)
                    : RemoteTarget?.Invoke(entry.Source.ClientId);
                string watchingName = watching.HasValue && Find(watching) is { } target ? target.Name : string.Empty;
                _dead[i] = new SplitScreenDeadPlayer(entry.Key, entry.Source, entry.Name, entry.SteamId, entry.Local, watching, watchingName, enhanced,
                    member.View, member.Following.HasValue ? DeadName(member.Following.Value) : string.Empty,
                    !entry.Local && member.Following.HasValue && member.Following == LocalClientId(),
                    !entry.Local && !Preview && HasWatchCycle(entry.Source.ClientId));
            }
            _voices.Clear();
            foreach (var player in _players) if (_game.VoiceLevel(player.Source) is var level && level > 0) _voices.Add((player.Source, level));
            foreach (var dead in _dead) if (_game.VoiceLevel(dead.Source) is var level && level > 0) _voices.Add((dead.Source, level));
            // Preview speech: one view and one audience member at a time take turns talking, their loudness rising
            // and falling like a sentence so the volume-following glow can be seen.
            if (Preview && _previewSpeaking)
            {
                float time = Time.unscaledTime;
                // The demo holds one speaker in each place so its zoom can frame them.
                int turn = DemoRunning ? 0 : (int)(time / 1.6f);
                float level = SplitScreenVoiceLevel.Intensity(true, .12f + .1f * Mathf.Sin(time * 7) + .06f * Mathf.Sin(time * 17));
                if (_players.Count > 0) _voices.Add((_players[turn % _players.Count].Key, level));
                if (_dead.Count > 0) _voices.Add((_dead[(turn + 1) % _dead.Count].Key, level));
            }
            _view.SetVoices(_voices, split.ShowSpeaking.Value);
            _view.SetDeadPlayers(split.ShowDeadBar.Value ? _dead : NoDead, _config.UseChineseText, SteamAvatarCache.Get);
            // Who watches the large view: mod players report their target, you watch your own, others are unknown.
            _viewerNames.Clear(); int unknown = 0;
            if (_focused.HasValue && split.ShowViewerCount.Value)
                foreach (var entry in _dead)
                {
                    if (!entry.Enhanced) unknown++;
                    else if (entry.Watching == _focused) _viewerNames.Add(entry.Local ? entry.Name + (_config.UseChineseText ? "（你）" : " (you)") : entry.Name);
                }
            _view.SetViewers(split.ShowViewerCount.Value ? _focused : null, _viewerNames, unknown, _config.UseChineseText);
            FollowWatchTogether();
        }
        _view.SetTiles(_players, _focused, _audio);
        _view.Tick(Time.unscaledDeltaTime);
        _spikeUiMs = (Stopwatch.GetTimestamp() - phaseStarted) * 1000.0 / Stopwatch.Frequency;
        phaseStarted = Stopwatch.GetTimestamp();
        _view.CopyCompletedRetirementsTo(_retired);
        foreach (var key in _retired) { _renderer.ReleaseView(key); _schedule.Remove(key); _crossfade.Remove(key); }
        _spikeCleanupMs = (Stopwatch.GetTimestamp() - phaseStarted) * 1000.0 / Stopwatch.Frequency;
        phaseStarted = Stopwatch.GetTimestamp();
        var primary = Find(_audio);
        _renderer.BindPrimary(primary, _focused.HasValue && _focused == _audio);
        _renderer.UpdatePrimaryPose();
        _renderer.MirrorPrimaryPose(MirrorPose(), _watchWith.HasValue);
        if (_focused.HasValue)
        {
            if (_preferredMode != _renderer.PrimaryMode || _preferredStyle != _config.Camera.MonitorStyle.Value)
            { _crossfade.Add(_focused.Value); _schedule.Invalidate(_focused.Value); }
            _preferredMode = _renderer.PrimaryMode; _preferredStyle = _config.Camera.MonitorStyle.Value;
        }
        _spikePoseMs = (Stopwatch.GetTimestamp() - phaseStarted) * 1000.0 / Stopwatch.Frequency;
        var c = _config.Camera.SplitScreen;
        var budget = SplitScreenRenderBudget.Resolve(c.Quality.Value, _players.Count, c.MainScale.Value, c.MainFps.Value, c.TileScale.Value, c.TileFps.Value,
            c.MainFollowsGame.Value);
        _metrics.Frame(Time.unscaledDeltaTime);
        _draws.Clear();
        phaseStarted = Stopwatch.GetTimestamp();
        _renderer.SetTileShadowCascades(budget.TileCascades);
        _renderer.FollowShip();
        _renderer.RefreshVisibleRooms(_players);
        _spikeSceneMs = (Stopwatch.GetTimestamp() - phaseStarted) * 1000.0 / Stopwatch.Frequency;
        var main = _takeoff ? ShipKey : _focused;
        _schedule.Select(_players, main, budget, Time.unscaledTimeAsDouble, _draws);
        // A view's first render at a new size costs several normal ones (HDRP sizes that camera's buffers then; one
        // logged spike: 11 ms against about 2), and a layout change resizes every view at once. So one view takes its
        // new size per frame, the large one first; the others keep their last picture, stretched to their new place
        // (all are 16:9), until their turn in the next frames. A view's first picture is never held back.
        bool resized = false;
        foreach (var key in _draws)
        {
            var player = Find(key);
            if (!player.HasValue || !_view.TryGetTargetSize(key, out var size)) continue;
            SplitScreenRenderBudget.Size(size.y, key == main ? budget.MainScale : budget.TileScale, out int width, out int height);
            var before = _renderer.GetTexture(key);
            bool newSize = before == null || before.width != width || before.height != height;
            if (newSize && before != null)
            {
                if (resized) { _schedule.Invalidate(key); continue; }
                resized = true;
            }
            bool transition = _crossfade.Contains(key);
            if (transition) _renderer.BeginViewTransition(key);
            long started = Stopwatch.GetTimestamp();
            var texture = key == ShipKey ? _renderer.RenderGameView(key, width, height) : _renderer.RenderView(player.Value, key == _audio, width, height);
            double cost = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            _metrics.RenderCost(cost, key != _audio);
            _spikeTotalRenderMs += cost * 1000;
            if (cost * 1000 > _spikeRenderMs) { _spikeRenderMs = cost * 1000; _spikeWidth = width; _spikeHeight = height; _spikeNewSize = newSize; }
            if (texture != null) { _view.SetTexture(key, texture, transition); _crossfade.Remove(key); _metrics.Rendered(key); }
        }
        _view.SetPrimaryTransitionOpacity(_renderer.PrimaryTransitionOpacity);
        phaseStarted = Stopwatch.GetTimestamp();
        _renderer.CollectRetiredTextures(_view.ReferencesTexture);
        _spikeCleanupMs += (Stopwatch.GetTimestamp() - phaseStarted) * 1000.0 / Stopwatch.Frequency;
        if (Time.unscaledTime >= _nextStatus)
        {
            _nextStatus = Time.unscaledTime + .5f;
            _presetText = SplitScreenRenderBudget.PresetName(c.Quality.Value, _players.Count, _config.UseChineseText);
            RebuildHints();
            var rates = _metrics.Sample(Time.unscaledTimeAsDouble, _players, _focused);
            _statusText = _config.UseChineseText
                ? $"{_renderer.CameraCount} 路 · 大窗 {rates.MainFps:F0} / 小窗 {rates.MinimumTileFps:F0}–{rates.MaximumTileFps:F0} FPS · 游戏 {rates.FrameMilliseconds:F1} ms · 绘制 CPU {rates.RenderMillisecondsPerFrame:F1} ms/帧（小窗每次 {rates.MillisecondsPerTile:F2} ms）· 纹理约 {_renderer.TextureBytes / 1048576f:F1} MiB"
                : $"{_renderer.CameraCount} cams · Focus {rates.MainFps:F0} / Tiles {rates.MinimumTileFps:F0}–{rates.MaximumTileFps:F0} FPS · Game {rates.FrameMilliseconds:F1} ms · Render CPU {rates.RenderMillisecondsPerFrame:F1} ms/frame ({rates.MillisecondsPerTile:F2} ms/tile) · Textures ~{_renderer.TextureBytes / 1048576f:F1} MiB";
        }
        // A key press can change the camera, its style or the thermal colours: the hints follow on the next frame.
        if (SpectatorInputService.AnyKeyOrButtonPressed()) _hintsStale = true;
        else if (_hintsStale) { _hintsStale = false; RebuildHints(); }
        // The preview shows them over its ESC menu too, so they can be reviewed alongside the toolbar.
        _view.SetKeyHints(_hintRows, accentRow: _voteHintRow, enabled: !DemoRunning && !_takeoff && _config.Camera.ShowKeyHints.Value && !LethalCompanySpectatorUiVisibility.Hidden && (Preview || !context.GameUiOpen));
        var pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : _lastPointer;
        // The hints stay while a cursor (ours or another mod's) is shown inside the game window, and go 3 s after it
        // is hidden or leaves the window.
        if (Cursor.visible && Application.isFocused && new Rect(0, 0, Screen.width, Screen.height).Contains(pointer)) _view.WakeKeyHints();
        _lastPointer = pointer;
        _view.UpdateStatus(_statusText, _config.UseChineseText, _players.Count, _population.Playing,
            _presetText, _focused ?? _audio,
            PreviewModeIndex(), _previewHudVisible, _config.Camera.MonitorInfrared.Value, _previewSpeaking, _population.Audience, _demoCaptions);
    }

    private void HandlePointer()
    {
        var mouse = Mouse.current;
        if (_view == null) return;
        if (mouse == null || (!Preview && !SpectatorPointerCapture.IsActive)) { _view.SetHover(null); _view.SetViewersHover(false); return; }
        Vector2 point = mouse.position.ReadValue();
        if (Preview && _view.HandleTestPointer(point, mouse.leftButton.wasPressedThisFrame)) { _view.SetHover(null); _view.SetViewersHover(false); return; }
        // The watch roster and emote picker above the views handle their own clicks.
        if (OwnsPointer?.Invoke(point) == true)
        { _view.SetHover(null); _view.SetDeadHover(null); return; }
        // The large view's viewer count lists who watches while the pointer rests on it.
        bool viewers = _view.HitTestViewers(point);
        _view.SetViewersHover(viewers);
        if (viewers) { _view.SetHover(null); _view.SetDeadHover(null); return; }
        // The audience row: hovering shows a player's card, clicking watches together with them.
        var audience = _view.HitTestDead(point);
        _view.SetDeadHover(audience?.Key);
        if (audience.HasValue)
        {
            _view.SetHover(null);
            if (mouse.leftButton.wasPressedThisFrame) ToggleWatchTogether(audience.Value);
            return;
        }
        var hit = _view.HitTest(point);
        _view.SetHover(hit);
        if (!hit.HasValue) return;
        if (Preview && mouse.rightButton.wasPressedThisFrame) { _population.Kill(hit.Value); return; }
        if (mouse.leftButton.wasPressedThisFrame) { StopWatchTogether(); Focus(_focused == hit ? null : hit); }
    }

    // Only split-screen players can be followed: a single-view spectator is neither seen nor heard in our views.
    private void ToggleWatchTogether(SplitScreenDeadPlayer player)
    {
        if (_takeoff || player.Local || !player.Enhanced || player.SplitView == Networking.SpectatorSplitView.None || player.FollowLoop) return;
        if (_watchWith == player.Key) { StopWatchTogether(); return; }
        if (!_watchWith.HasValue) _modeBeforeFollow = (_preferredMode, _preferredStyle);
        _watchWith = player.Key; _watchTarget = null;
        FollowWatchTogether();
    }

    private string DeadName(ulong clientId)
    {
        foreach (var entry in _dead) if (entry.Source.ClientId == clientId) return entry.Name;
        return string.Empty;
    }
    private ulong? LocalFollowingId()
    {
        if (_watchWith.HasValue) foreach (var entry in _dead) if (entry.Key == _watchWith.Value) return entry.Source.ClientId;
        return null;
    }
    /// <summary>The audience member this player follows (client id), published with the pose; a preview is local only.</summary>
    internal static ulong? LocalFollowing => Current is { Active: true, Preview: false } module ? module.LocalFollowingId() : null;

    private void StopWatchTogether(bool ownCamera = true)
    {
        if (!_watchWith.HasValue) return;
        _watchWith = _watchTarget = null; _mirrorFrom = null;
        _view?.SetWatchTogether(null, null);
        // Our own camera again, as chosen before following (the vanilla view, third person, ...), recentred.
        if (_mirrored)
        {
            var (mode, style) = _modeBeforeFollow;
            if (ownCamera && _focused.HasValue) { SelectMode(mode, style); SpectatorFreecamController.Current?.RecenterView(); }
            else { _preferredMode = mode; _preferredStyle = style; _config.Camera.MonitorStyle.Value = style; }
        }
        _mirrored = false;
    }

    // We mirror the followed player's split-screen state: the view they enlarge becomes our large view (and audio),
    // and when they return to the tiles we return to the audience with them, so both stay in the same voice group.
    // In their large view we watch through their camera itself: the same kind of camera, posed where theirs is, as
    // it turns and moves (freecam flying included), our own camera input ignored, until we stop following.
    private void FollowWatchTogether()
    {
        if (!_watchWith.HasValue || _view == null) return;
        SplitScreenDeadPlayer? leader = null;
        foreach (var entry in _dead) if (entry.Key == _watchWith.Value) leader = entry;
        if (!leader.HasValue || leader.Value.SplitView == Networking.SpectatorSplitView.None || leader.Value.FollowLoop) { StopWatchTogether(); return; }
        _mirrorFrom = null;
        if (leader.Value.SplitView == Networking.SpectatorSplitView.Audience)
        {
            // Tiled with them, and their audio view becomes our orange one.
            var listening = leader.Value.Watching;
            bool moved = _focused.HasValue || listening.HasValue && Contains(listening.Value) && _audio != listening;
            _watchTarget = null;
            if (listening.HasValue && Contains(listening.Value)) _audio = listening;
            if (moved) { Focus(null); _view.PulseWatchTogether(); }
            _view.SetWatchTogether(_watchWith, _config.UseChineseText ? $"正在跟随 {leader.Value.Name}（观众席）" : $"Following {leader.Value.Name} (audience)", _audio);
            return;
        }
        var target = leader.Value.Watching;
        _view.SetWatchTogether(_watchWith, _config.UseChineseText ? $"正在与 {leader.Value.Name} 一起观看" : $"Watching along with {leader.Value.Name}",
            target.HasValue && Contains(target.Value) ? target : _focused);
        if (target.HasValue && Contains(target.Value) && (_watchTarget != target || _focused != target))
        {
            bool followed = _watchTarget.HasValue;
            _watchTarget = target;
            Focus(target);
            if (followed) _view.PulseWatchTogether();
        }
        _mirrorFrom = leader.Value.Source.ClientId;
        if (FollowedCamera() is { } camera && (camera.Mode != _preferredMode || camera.Mode == SpectatorCameraMode.Monitor && camera.Style != _preferredStyle))
            SelectMode(camera.Mode, camera.Mode == SpectatorCameraMode.Monitor ? camera.Style : null);
    }

    // The camera of the chain's end (a follower's own camera is not what it shows), once it shows our large view.
    private (Vector3 Position, Quaternion Rotation, SpectatorCameraMode? Mode, int Style, SplitScreenKey? Target, float FieldOfView)? FollowedCamera()
    {
        if (!_mirrorFrom.HasValue || !_focused.HasValue || _watchTarget != _focused) return null;
        var camera = RemoteCamera?.Invoke(SpectatorPresence.SpectatorPartyRules.Leader(_mirrorFrom.Value, _remoteFollowing));
        return camera?.Target == _focused ? camera : null;
    }

    // Every frame: their poses arrive a few times a second, and the renderer eases between them.
    private (Vector3 Position, Quaternion Rotation, float FieldOfView)? MirrorPose()
    {
        if (FollowedCamera() is not { } camera) return null;
        _mirrored = true;
        return (camera.Position, camera.Rotation, camera.FieldOfView);
    }

    // Concurrent follow clicks can form a loop after our first click. Recheck the published chain at each
    // audience refresh so delayed layouts do not echo around that loop indefinitely.
    // Also marks audience members that cannot be followed (their chain leads back to us) before anyone clicks.
    private bool HasWatchCycle(ulong leader)
    {
        if (Preview) return false; // Preview seats share a synthetic source and do not publish follow links.
        _followPath.Clear();
        if (LocalClientId() is { } local) _followPath.Add(local);
        for (ulong? next = leader; next.HasValue; next = RemoteSplit?.Invoke(next.Value).Following)
            if (!_followPath.Add(next.Value)) return true;
        return false;
    }
    private ulong? LocalClientId()
    {
        foreach (var entry in _dead) if (entry.Local) return entry.Source.ClientId;
        return null;
    }

    // Toolbar order: vanilla, freecam, third, first, cinema, travelling, monitor; -1 while the grid has no focused view.
    private int PreviewModeIndex()
    {
        if (!_focused.HasValue) return -1;
        // The camera as drawn: a choreography selection outdoors shows (and lights) the vanilla view.
        var mode = EffectiveMode;
        return mode == null ? 0 : mode == SpectatorCameraMode.Monitor ? _preferredStyle == 2 ? 5 : 6 : (int)mode.Value + 1;
    }

    private void Focus(SplitScreenKey? key)
    {
        if (key.HasValue && (!Contains(key.Value) || _takeoff)) return;
        if (_focused.HasValue) { _crossfade.Add(_focused.Value); _schedule.Invalidate(_focused.Value); }
        if (_focused != key) { _view?.WakeKeyHints(); _layoutChangedAt = Time.unscaledTime; }
        _focused = key;
        if (key.HasValue)
        {
            _audio = key; _crossfade.Add(key.Value); _schedule.Invalidate(key.Value);
            _renderer.BindPrimary(Find(key), true);
            _renderer.SetPrimaryMode(_preferredMode, _preferredStyle);
        }
        else _renderer.BindPrimary(Find(_audio), false);
    }

    private void SelectMode(SpectatorCameraMode? mode, int? style = null)
    {
        if (!_focused.HasValue) Focus(_audio ?? (_players.Count > 0 ? _players[0].Key : null));
        if (!_focused.HasValue) return;
        _preferredMode = mode; if (style.HasValue) _preferredStyle = style.Value;
        _renderer.SetPrimaryMode(mode, _preferredStyle); _crossfade.Add(_focused.Value); _schedule.Invalidate(_focused.Value);
        SpectatorFreecamController.Current?.SuppressModeInputThisFrame();
    }

    private bool TryReadModeKey(out SpectatorCameraMode? mode)
    {
        bool Pressed(KeyCode key) => SpectatorInputService.IsKeyPressedThisFrame(key);
        mode = null;
        if (Pressed(_config.ResetToVanillaViewKey.Value)) return true;
        if (Pressed(_config.ToggleFreecamKey.Value) && _config.EnableFreecam.Value) { mode = SpectatorCameraMode.Freecam; return true; }
        if (Pressed(_config.ToggleThirdPersonKey.Value) && _config.EnableThirdPerson.Value) { mode = SpectatorCameraMode.ThirdPerson; return true; }
        if (Pressed(_config.Camera.FirstPersonKey.Value)) { mode = SpectatorCameraMode.FirstPerson; return true; }
        if (Pressed(_config.Camera.CinematicKey.Value)) { mode = SpectatorCameraMode.Cinematic; return true; }
        if (Pressed(_config.Camera.MonitorKey.Value) && MonitorAvailable) { mode = SpectatorCameraMode.Monitor; return true; }
        return false;
    }

    private void OnTestAction(SplitScreenTestAction action, int value, SplitScreenKey? selected)
    {
        switch (action)
        {
            case SplitScreenTestAction.SetCount: _population.SetCount(value); break;
            case SplitScreenTestAction.KillSelected: if (selected.HasValue) _population.Kill(selected.Value); break;
            case SplitScreenTestAction.KillRandom: _population.KillRandom(); break;
            case SplitScreenTestAction.Revive: _population.RestoreOne(); break;
            case SplitScreenTestAction.Reset: _population.SetCount(31); _population.Playing = false; break;
            case SplitScreenTestAction.TogglePlayback:
                _population.Playing = !_population.Playing; _nextDeath = Time.unscaledTime + 1;
                if (_population.Playing && _population.Count == 0) _population.SetCount(31);
                break;
            case SplitScreenTestAction.SelectMode:
                SelectMode(value == 0 ? null : value >= 5 ? SpectatorCameraMode.Monitor : (SpectatorCameraMode)(value - 1), value >= 5 ? value == 5 ? 2 : 0 : null);
                break;
            case SplitScreenTestAction.CycleQuality:
                var c = _config.Camera.SplitScreen; c.Quality.Value = (SplitScreenQuality)(((int)c.Quality.Value + 1) % 4);
                _schedule.Clear(); _metrics.Reset(Time.unscaledTimeAsDouble); _nextStatus = Time.unscaledTime + .5f;
                _presetText = SplitScreenRenderBudget.PresetName(c.Quality.Value, _players.Count, _config.UseChineseText); break;
            case SplitScreenTestAction.ToggleHud: _previewHudVisible = !_previewHudVisible; break;
            case SplitScreenTestAction.ToggleThermal: _config.Camera.MonitorInfrared.Value = !_config.Camera.MonitorInfrared.Value; break;
            case SplitScreenTestAction.Exit: _exitPreviewRequested = true; break;
            case SplitScreenTestAction.ToggleSpeaking: _previewSpeaking = !_previewSpeaking; break;
            case SplitScreenTestAction.SetAudience: _population.SetAudience(value); break;
            case SplitScreenTestAction.Demo: StartDemo(value); break;
            case SplitScreenTestAction.ToggleCaptions: _demoCaptions = !_demoCaptions; break;
            case SplitScreenTestAction.ShowReport: OpenReportPreview(); break;
            case SplitScreenTestAction.ShowLeaveVote:
                // The toolbar fades out for the demonstration; the key hints show its prompt.
                _votePreviewAt = Time.unscaledTime; ShowPreviewPanel(); _view?.WakeKeyHints(); break;
            default: SocialTest?.Invoke(action); break;
        }
    }

    private bool Contains(SplitScreenKey key) => Find(key).HasValue;
    private SplitScreenParticipant? Find(SplitScreenKey? key)
    { if (key.HasValue) foreach (var player in _players) if (player.Key == key.Value) return player; return null; }

    private void FinishPreview(bool canResume)
    {
        if (!_previewReturn.HasValue) return;
        var saved = _previewReturn.Value;
        Stop(); _previewReturn = null;
        _preferredMode = saved.Mode; _preferredStyle = saved.Style;
        _config.Camera.MonitorStyle.Value = saved.ConfigStyle;
        _config.Camera.SplitScreen.Quality.Value = saved.Quality;
        _config.Camera.MonitorInfrared.Value = saved.Thermal;
        _config.Camera.ThermalPalette.Value = saved.ThermalPalette;
        if (!canResume || !saved.Active || !Start(false)) return;
        _focusPending = false;
        _game.CopyLivingPlayersTo(_players, false);
        _audio = _game.Read().Target;
        if (saved.Focused.HasValue && Contains(saved.Focused.Value)) Focus(saved.Focused);
        _pointerRequested = saved.Pointer;
    }

    private readonly struct PreviewReturnState
    {
        internal readonly bool Active, Pointer, Thermal;
        internal readonly SplitScreenKey? Focused;
        internal readonly SpectatorCameraMode? Mode;
        internal readonly int Style, ConfigStyle, ThermalPalette;
        internal readonly SplitScreenQuality Quality;
        internal PreviewReturnState(SplitScreenModule module)
        {
            Active = module.Active; Pointer = module._pointerRequested;
            Focused = module._focused; Mode = module._preferredMode; Style = module._preferredStyle;
            ConfigStyle = module._config.Camera.MonitorStyle.Value;
            Quality = module._config.Camera.SplitScreen.Quality.Value;
            Thermal = module._config.Camera.MonitorInfrared.Value;
            ThermalPalette = module._config.Camera.ThermalPalette.Value;
        }
    }

    // A forced stop keeps the last picture over the game's: as the ship leaves with the report to come, until the report
    // covers it; while still dead otherwise (a scene loading as the ship lands, a camera hand-off), until the
    // split-screen opens again. The game's own view does not flash up in between.
    private bool Freezes(SplitScreenSessionContext context)
    {
        if (!Active || Preview || _view == null || !context.Dead) return false;
        _frozenForReport = context.ShipLeaving; _frozenAt = Time.unscaledTime;
        return !context.ShipLeaving || _reportArmed && LethalCompanyRoundResults.ReportComing;
    }

    private void Thaw()
    {
        if (!_frozen) return;
        _frozen = false;
        LethalCompanySplitScreenUiPresentation.MenuOver = false;
        _view?.SetVisible(false); _view?.Clear(); _view?.CopyCompletedRetirementsTo(_retired);
        _renderer.CollectRetiredTextures(_ => false);
    }

    private void Stop() => Stop(false);
    private void Stop(bool freeze)
    {
        if (Active && !Preview && _reportArmed && _report?.Visible != true)
        {
            // The scene unload will destroy the per-view textures before FillEndGameStats runs.
            var still = LastViewTexture();
            if (still != null) { EnsureReport(); _report!.CaptureBackdrop(still); }
        }
        if (!Active) return;
        if (freeze)
        {
            // Over everything (the game's HUD too), as it was last drawn; its textures stay until the thaw.
            _frozen = true; _view!.SetBeneath(null); _renderer.Restore(_view.ReferencesTexture);
        }
        else { Thaw(); _view?.SetVisible(false); _view?.Clear(); _view?.CopyCompletedRetirementsTo(_retired); _renderer.Restore(); }
        // Releasing cameras does not release a surface still hidden for the report's menu/chat handoff.
        if (_report?.DrawsBeneathHud != true) _game.HudCanvasBeneath(false);
        SpectatorVanillaInputGuard.SplitScreenActive = false;
        var stoppedIn = _game.Read();
        LethalCompanySpectatorRosterCursor.Shared.Release(stoppedIn.InputBlocked);
        // Closed by a revive: the game's spectating UI, fading out with its death screen, is not shown meanwhile.
        if (!Preview && !stoppedIn.Dead) LethalCompanySplitScreenUiPresentation.HideSpectateUiUntil = Time.unscaledTime + RevivedSpectateUiSeconds;
        LethalCompanySpectatorRosterCursor.SplitScreenOwnsInput = false;
        if (DemoRunning) ReleaseDemo();
        Active = Preview = false; _focused = _audio = null; _votePreviewAt = -1; _population.Playing = false; _watchWith = _watchTarget = null; _mirrorFrom = null; _mirrored = false; _takeoff = false;
        LethalCompanySplitScreenUiPresentation.Takeoff = LethalCompanySplitScreenUiPresentation.MenuOver = false;
        if (_reportPreviewAt >= 0) HideReport();
        _players.Clear(); _sources.Clear(); _schedule.Clear(); _crossfade.Clear();
    }
    public void Dispose()
    { HideReport(); FinishPreview(false); Stop(); Thaw(); _view?.Dispose(); _view = null; _report?.Dispose(); _report = null; _renderer.Dispose();
        LethalCompanySplitScreenUiPresentation.Clear(); if (Current == this) Current = null; }

    // The game's end-of-round report. Whoever is in split-screen when the game shows it (the ship-takeoff view
    // included) gets it in split-screen style, through to the game's own end of it after the revive; with that
    // style off, the game's (or another mod's) report draws above the views instead.
    private void TickReport(SplitScreenSessionContext context)
    {
        // A report keeps the style it started in: ours when the split-screen had this round as the game showed it,
        // else the game's to its end. Observe native starts even with our style/feature switched off, so enabling
        // either later cannot replace an already running report.
        if (_reportRound != LethalCompanyRoundResults.Round && LethalCompanyRoundResults.Open && LethalCompanyRoundResults.Shown)
        {
            _reportRound = LethalCompanyRoundResults.Round;
            // With diagnostics on, one line a round on who shows it and when, to trace a report reported to show the game's first.
            string state = $"split-screen {(Active ? "on" : "off")}, armed {_reportArmed}, styled {_config.Camera.SplitScreen.StyledReport.Value}, preview {Preview}";
            if (_config.EnableEnhancedSpectator.Value && !Preview && (_reportArmed || Active)
                && _config.Camera.SplitScreen.StyledReport.Value && !LethalCompanyRoundResults.DayEnded)
            {
                _reportArmed = true;
                LethalCompanyRoundResults.Read(_reportData);
                OpenReport();
                ModLog.Debug($"Split-screen round report opened {Time.frameCount - LethalCompanyRoundResults.ShownFrame} frames "
                    + $"({(Time.unscaledTime - LethalCompanyRoundResults.ShownAt) * 1000:F0} ms) after the game started its report ({state}).");
            }
            else ModLog.Debug($"The game's own round report shows this round ({state}).");
        }
        if (!_config.EnableEnhancedSpectator.Value) return;
        if (!Preview && !_config.Camera.SplitScreen.StyledReport.Value) { HideReport(); return; }
        if (_report == null || !_report.Visible)
        {
            _reportPreviewAt = -1;
            LethalCompanySplitScreenUiPresentation.ReportOwned = LethalCompanySplitScreenUiPresentation.ReportBeneath = false;
            if (_reportArmed && !context.ShipLeaving && !LethalCompanyRoundResults.Open) HideReport();
            return;
        }
        // A preview dismissed by a click plays none of its later sounds while it closes.
        if (_reportPreviewAt >= 0) { if (!_report.Closing) TickReportPreview(); }
        // The day passing starts the game's days-left banner and its sound (and fades its penalty box): the report
        // hands over to it then, rather than covering it until the HUD returns.
        else if (LethalCompanyRoundResults.Open && !LethalCompanyRoundResults.DayEnded) LethalCompanyRoundResults.Read(_reportData);
        else _report.Close();
        // The game's menu and chat stay usable over it.
        _report.SetBeneath(!Preview && context.GameUiOpen ? LethalCompanySplitScreenState.HudCanvas : null);
        _report.Tick(_reportData, Time.unscaledDeltaTime);
        // Revived before the report ends: its backdrop turns from the still into the live view, blurred alike.
        _report.SetLive(!Preview && !context.Dead ? LethalCompanySplitScreenState.LiveView : null, Time.unscaledDeltaTime);
        // The report now covers the frozen picture; it can go.
        if (_report.Covering) Thaw();
        // Keep native/third-party reports suppressed through our closing animation, including after revival.
        LethalCompanySplitScreenUiPresentation.ReportOwned = !Preview && _report.Visible;
        LethalCompanySplitScreenUiPresentation.ReportBeneath = LethalCompanySplitScreenUiPresentation.ReportOwned && _report.DrawsBeneathHud;
        LethalCompanySplitScreenUiPresentation.ChatTyped = context.Chat;
    }

    private void EnsureReport()
    {
        _report ??= new SplitScreenResultsPanel { AvatarOf = SteamAvatarCache.Get };
        _report.ConfigureFont(LethalCompanySplitScreenState.Font);
    }

    private Texture? LastViewTexture()
    {
        var still = _takeoff ? ShipKey : _focused ?? _audio ?? (_players.Count > 0 ? _players[0].Key : (SplitScreenKey?)null);
        return still.HasValue ? _renderer.GetTexture(still.Value) : null;
    }

    private void OpenReport()
    {
        EnsureReport();
        _report!.Open(_reportData, LastViewTexture(), _config.UseChineseText);
        ModLog.Debug("Split-screen round report opened.");
    }

    private void HideReport()
    {
        Thaw();
        _report?.Hide();
        if (!Active) _game.HudCanvasBeneath(false);
        _reportArmed = false;
        LethalCompanySplitScreenUiPresentation.ReportOwned = LethalCompanySplitScreenUiPresentation.ReportBeneath = false;
        _reportPreviewAt = -1;
        if (_viewCovered) { _viewCovered = false; if (Active) _view?.SetVisible(true); }
    }

    // The preview's report: its people, then the level and the penalty as the game would bring them, then it closes.
    private void OpenReportPreview()
    {
        _reportPeople.Clear();
        _population.Chinese = _config.UseChineseText;
        _population.CopyPeopleTo(_sources, _previewPeople);
        foreach (var person in _previewPeople) _reportPeople.Add((person.Name, person.Alive));
        if (_reportPeople.Count == 0) _reportPeople.Add((_config.UseChineseText ? "测试玩家" : "Preview player", true));
        _reportData.FillPreview(_reportPeople, LethalCompanyRoundResults.PlanetName, _config.UseChineseText);
        OpenReport();
        _reportPreviewAt = Time.unscaledTime; _previewMusic = _previewLevelUp = _previewLevelDrop = _previewLevelDown = false;
    }

    // The preview's early-leave vote, in the game's order: the prompt, this player's three-second hold and vote, the
    // others' votes (one a second, quicker for a crowd; one per view, at least two), then the departure for a while.
    private float _votePreviewAt = -1;
    private const float VotePreviewHoldAt = .6f, VotePreviewHoldSeconds = 3, VotePreviewDepartureSeconds = 4;
    private SplitScreenLeaveVoteState PreviewLeaveVote()
    {
        int needed = Math.Max(2, _players.Count);
        // Between demonstrations the vote is on offer, as for a spectator who has not voted: the key hints carry it.
        if (_votePreviewAt < 0) return new SplitScreenLeaveVoteState(true, false, false, 0, needed, 0, null);
        float t = Time.unscaledTime - _votePreviewAt, voted = VotePreviewHoldAt + VotePreviewHoldSeconds;
        float interval = Math.Min(1f, 6f / needed), passed = voted + (needed - 1) * interval + .8f;
        if (t >= passed + VotePreviewDepartureSeconds)
        {
            // The toolbar fades back in as the strip goes.
            _votePreviewAt = -1; ShowPreviewPanel();
            return new SplitScreenLeaveVoteState(true, false, false, 0, needed, 0, null);
        }
        if (t >= passed) return new SplitScreenLeaveVoteState(false, false, false, needed, 0, 0, LethalCompanyLeaveVote.PreviewDeparture());
        if (t < voted) return new SplitScreenLeaveVoteState(true, false, false, 0, needed, Mathf.Clamp01((t - VotePreviewHoldAt) / VotePreviewHoldSeconds), null);
        int votes = Math.Min(needed, 1 + (int)((t - voted) / interval));
        return new SplitScreenLeaveVoteState(true, true, false, votes, needed, 0, null);
    }

    private void TickReportPreview()
    {
        float t = Time.unscaledTime - _reportPreviewAt;
        bool chinese = _config.UseChineseText;
        if (t >= PreviewMusicAt && !_previewMusic) { _previewMusic = true; LethalCompanyRoundSounds.Play(RoundReportSound.Music); }
        if (t >= PreviewLevelAt && !_reportData.Level)
        {
            _reportData.Level = true; _reportData.LevelName = chinese ? "实习生" : "Intern";
            LethalCompanyRoundSounds.Play(RoundReportSound.Experience);
        }
        if (_reportData.Level)
        {
            // Experience rises past the rank (a level-up); after the penalty the preview also shows a level-down, the
            // experience falling back below it. The game's first ranks: Intern below 25, Part-timer from 25 to 75.
            float xp = 18 + 12 * Mathf.Clamp01((t - PreviewLevelAt) / 1.5f);
            if (t >= PreviewLevelDownAt)
            {
                if (!_previewLevelDrop) { _previewLevelDrop = true; LethalCompanyRoundSounds.Play(RoundReportSound.ExperienceDown); }
                xp -= 8 * Mathf.Clamp01((t - PreviewLevelDownAt) / 1.2f);
            }
            bool second = xp >= 25;
            if (second && !_previewLevelUp) { _previewLevelUp = true; LethalCompanyRoundSounds.Play(RoundReportSound.LevelUp); }
            if (!second && _previewLevelUp && !_previewLevelDown) { _previewLevelDown = true; LethalCompanyRoundSounds.Play(RoundReportSound.LevelDown); }
            _reportData.LevelName = second ? "Part-timer" : "Intern";
            _reportData.LevelFill = second ? (xp - 25) / 50 : xp / 25;
            _reportData.LevelExperience = Mathf.RoundToInt(xp) + " EXP";
        }
        if (t >= PreviewPenaltyAt && !_reportData.Penalty)
        {
            _reportData.Penalty = true;
            _reportData.PenaltyLines = chinese ? "2 名伤亡：-40%\n（回收 0 具遗体）" : "2 casualties: -40%\n(0 bodies recovered)";
            _reportData.PenaltyDue = chinese ? "应付：$212" : "DUE: $212";
        }
        if (t >= PreviewDayAt) { LethalCompanyRoundSounds.Play(RoundReportSound.DaysLeft); _report!.Close(); }
    }
    private readonly List<(int Slot, string Name, bool Alive)> _previewPeople = new();
}

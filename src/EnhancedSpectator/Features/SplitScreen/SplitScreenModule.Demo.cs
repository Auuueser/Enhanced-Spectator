using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Logging;
using UnityEngine;

namespace EnhancedSpectator.Features.SplitScreen;

/// <summary>
/// The preview's demo: a short animated tour that tells a player what each feature does for them. Every chapter
/// opens with a card saying what the feature is for; its steps then run through the same operations as the test
/// toolbar and the real panels. A step first moves the picture onto its subject (the rest dimmed) and the simulated
/// pointer onto what it uses; only when both have arrived does it act (click, hover, send), so what it shows is
/// never missed, and its caption then stays up for the step's reading time. The toolbar, menu and pointer go away
/// while it plays (clean for recordings); ESC stops it and the preview's state is put back.
/// </summary>
internal sealed partial class SplitScreenModule
{
    private const float CardSeconds = 2.8f;

    private sealed class DemoStep
    {
        internal readonly float Seconds;
        internal readonly string Zh, En;
        internal readonly Action Run;
        // What to zoom onto while the step plays (read every frame, as layouts move); null shows the whole picture.
        internal Func<Rect?>? Focus;
        // Where the pointer goes first; the step runs once it is there, after a click when Click is set.
        internal Func<Rect?>? Pointer;
        internal bool Click;
        // The key the step stands for, shown as a key cap in the caption.
        internal Func<string>? Key;
        internal DemoStep(float seconds, string zh, string en, Action run) { Seconds = seconds; Zh = zh; En = en; Run = run; }
        internal DemoStep Zoom(Func<Rect?> focus) { Focus = focus; return this; }
        internal DemoStep Hover(Func<Rect?> target) { Pointer = target; return this; }
        internal DemoStep Press(Func<Rect?> target) { Pointer = target; Click = true; return this; }
        internal DemoStep Keyed(Func<string> key) { Key = key; return this; }
    }

    private sealed class DemoChapter
    {
        internal readonly string Zh, En, WhatZh, WhatEn;
        internal readonly Action Setup;
        internal readonly DemoStep[] Steps;
        // Bet results show only in the chapter about bets; deaths and round ends elsewhere would pop them up.
        internal bool Bets;
        internal DemoChapter(string zh, string en, string whatZh, string whatEn, Action setup, params DemoStep[] steps)
        { Zh = zh; En = en; WhatZh = whatZh; WhatEn = whatEn; Setup = setup; Steps = steps; }
    }

    // A null step is the chapter's opening card.
    private readonly List<(int Chapter, DemoStep? Step)> _demo = new();
    private readonly List<SplitScreenParticipant> _demoTeammates = new(31);
    private DemoChapter[]? _chapters;
    private int _demoIndex, _demoChapter = -1;
    private float _demoWait;
    private bool _demoCaptions = true;
    private (int Count, int Audience, bool Speaking, bool Thermal, bool ThermalAll, SpectatorCameraMode? Mode, int Style) _demoSaved;
    private Func<Rect?>? _demoFocus;
    private Rect? _demoLastFocus;
    private float _demoMissing;
    private Action? _demoPending;
    private Vector2 _demoPoint;
    private SplitScreenKey? _demoKey;

    internal bool DemoRunning => _demo.Count > 0;
    internal int DemoChapterCount => Chapters.Length;
    /// <summary>
    /// While the demo plays, the pointer the panels react to: the simulated one, or a point off screen while it is
    /// hidden (the panels then behave as with a pointer, without "press P" hints). Null outside the demo.
    /// </summary>
    internal Vector2? DemoPointer => DemoRunning ? _view?.DemoCursorScreen ?? new Vector2(-1, -1) : null;
    /// <summary>The demo is on a chapter other than the bets: decided markets show no result card.</summary>
    internal bool DemoHidesBetResults => DemoRunning && !(_demoChapter >= 0 && Chapters[_demoChapter].Bets);
    /// <summary>The social panels' elements for the demo (unzoomed screen pixels), and a click on one of them.</summary>
    internal Func<SplitScreenDemoTarget, int, int, Rect?>? SocialFocus;
    internal Action<Vector2>? SocialClick;

    private DemoChapter[] Chapters => _chapters ??= new[]
    {
        new DemoChapter("分屏观战", "Split-screen",
            "阵亡后不用来回切换，同时看到每一位还活着的队友", "After you die, see every living teammate at once, no cycling through them",
            () => Baseline(4, 0),
            Step(2.6f, "每位存活的队友都有一个实时窗口", "Every living teammate gets a live view", () => { }),
            Step(2.6f, "人再多也不乱：窗口自动排布，最多 31 个", "However many there are, the views arrange themselves, up to 31", () => _population.SetCount(16)),
            Step(2.6f, "点击任意窗口放大，其余排到一旁", "Click any view to enlarge it; the rest line up beside it", () => FocusSlot(0))
                .Press(() => _view?.FocusTile(PreviewKey(0))),
            Step(3, "橙色标记：你现在听到的是他那边的声音", "The orange mark shows whose sound you are hearing", () => { })
                .Zoom(() => _view?.FocusTileCorner(PreviewKey(0))),
            Step(2.2f, "再点一次回到平铺", "Click it again to go back to the grid", () => Focus(null))
                .Press(() => _view?.FocusTile(PreviewKey(0))),
            Step(3, "有队友阵亡时，他的窗口淡出，其余自动补位", "When a teammate dies their view fades out and the rest close the gap", () => _population.KillRandom())),
        new DemoChapter("视角与热成像", "Cameras and thermal",
            "放大的窗口能像平时观战一样切换视角，还能开热成像", "The enlarged view switches cameras like normal spectating, and thermal makes people stand out",
            () => { Baseline(4, 0); Focus(TeammateView() ?? PreviewKey(0)); },
            Step(3, "第一人称：从队友的眼睛看出去", "First person: through your teammate's eyes", () => SelectMode(SpectatorCameraMode.FirstPerson))
                .Zoom(LargeView).Keyed(() => Key(_config.Camera.FirstPersonKey.Value)),
            // Third person is your own ghost (or the fear model you picked) flying free with the camera behind it;
            // the demo floats it in front of the watched player, facing them.
            Step(3.2f, "自身第三人称：变成鬼魂自由飞行，从它身后看队友", "Self third person: fly your own ghost and watch from behind it", () => { SelectMode(SpectatorCameraMode.ThirdPerson); _renderer.FacePreviewGhost(); })
                .Zoom(LargeView).Keyed(() => Key(_config.ToggleThirdPersonKey.Value)),
            // Every view in thermal: the small views draw each watched body from behind (your own too in a solo
            // preview), so the heat outlines show even without other players.
            Step(3.6f, "热成像：画面里的人和怪物一眼就能找到，可设为所有窗口", "Thermal: people and monsters stand out at a glance, in every view if you like",
                () => { Focus(null); _config.Camera.SplitScreen.ThermalAllViews.Value = true; _config.Camera.MonitorInfrared.Value = true; })
                .Keyed(() => Key(_config.Camera.MonitorInfraredKey.Value)),
            Step(2, "再按一次关闭", "Press it again to turn it off", () => _config.Camera.MonitorInfrared.Value = false)
                .Keyed(() => Key(_config.Camera.MonitorInfraredKey.Value))),
        new DemoChapter("谁在说话", "Who is speaking",
            "不用猜是谁在说话：他的窗口和观众席头像都会亮起", "No guessing who is talking: their view and their audience seat light up",
            () => Baseline(6, 6),
            Step(3.2f, "说话的人，名牌边缘随声音闪动", "A speaker's name plate pulses with their voice", () => _previewSpeaking = true)
                .Zoom(() => _view?.FocusTileCorner(PreviewKey(0))),
            Step(3.2f, "观众席里说话的人，头像随音量明暗", "In the audience row, an avatar glows with how loud they talk", () => { })
                .Zoom(() => _view?.FocusAudience()),
            Step(2, "安静下来，提示随之淡出", "When they go quiet the cue fades", () => _previewSpeaking = false)
                .Zoom(() => _view?.FocusAudience())),
        new DemoChapter("观众席", "The audience",
            "阵亡的队友坐在下方观众席：谁在看谁一目了然，还能跟他一起看", "The fallen sit in the audience row: see who watches whom, and watch along with them",
            () => Baseline(5, 8),
            Step(2.4f, "每位阵亡的队友一个头像", "One avatar for each fallen teammate", () => { })
                .Zoom(() => _view?.FocusAudience()),
            Step(2.8f, "光标移上去，看他正在看谁", "Hover an avatar to see whom they are watching", () => Hover(WatchableKey()))
                .Hover(() => AudienceChip(WatchableKey())).Zoom(() => HoveredMember() ?? _view?.FocusAudience()),
            Step(2.6f, "没装本模组的玩家只显示名字", "Players without the mod show just their name", () => Hover(AudienceKey(enhanced: false)))
                .Hover(() => AudienceChip(AudienceKey(enhanced: false))).Zoom(() => HoveredMember() ?? _view?.FocusAudience()),
            Step(3, "点击头像一起看：你的大窗口跟着他的视角走", "Click an avatar to watch along: your large view follows theirs", () => { Hover(null); WatchAlong(); })
                .Press(() => AudienceChip(WatchableKey())).Zoom(() => _view?.FocusBadge() ?? _view?.FocusAudience()),
            Step(3, "左上角显示有几人在看这个窗口，移上去看是谁", "Top left shows how many watch this view; hover to see who", () => _view?.SetViewersHover(true))
                .Hover(() => _view?.FocusViewers()).Zoom(() => _view?.FocusViewers()),
            Step(2, "再点一次头像，停止一起看", "Click the avatar again to stop", () => { _view?.SetViewersHover(false); StopWatchTogether(); })
                .Press(() => AudienceChip(WatchableKey())).Zoom(() => _view?.FocusAudience())),
        new DemoChapter("表情", "Emotes",
            "阵亡了也能和大家互动：发个表情，所有人都看得到", "Still part of the fun after death: send an emote everyone can see",
            () => Baseline(4, 6),
            // One frame for the whole chapter: the row, the emote bar above it and the bubbles over the seats.
            Step(1.8f, "按键打开表情栏", "Press the key to open the emotes", () => SocialTest?.Invoke(SplitScreenTestAction.OpenEmotes))
                .Keyed(() => Key(_config.Camera.EmoteKey.Value)).Zoom(EmoteRoom),
            Step(2.6f, "按数字键或点击发送：表情出现在你的头像上方", "Press a number or click to send: it pops above your seat", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.Emote, 1)).Zoom(EmoteRoom),
            Step(1.1f, "其他人的表情，出现在他们的头像上方", "Other people's emotes pop above their seats", () => SocialTest?.Invoke(SplitScreenTestAction.AudienceEmote))
                .Zoom(EmoteRoom),
            Step(1.1f, "其他人的表情，出现在他们的头像上方", "Other people's emotes pop above their seats", () => SocialTest?.Invoke(SplitScreenTestAction.AudienceEmote))
                .Zoom(EmoteRoom),
            Step(2.4f, "其他人的表情，出现在他们的头像上方", "Other people's emotes pop above their seats", () => SocialTest?.Invoke(SplitScreenTestAction.AudienceEmote))
                .Zoom(EmoteRoom),
            Step(3, "装了本模组的队友，在你的鬼魂模型上方也能看到；3 秒冷却防刷屏", "Teammates with the mod also see it above your ghost; a 3-second cooldown stops spam",
                () => SocialTest?.Invoke(SplitScreenTestAction.CloseSocial))),
        new DemoChapter("观众竞猜", "Audience bets",
            "阵亡了也有事做：用虚拟积分押谁下一个阵亡、有没有人活着离开（由房主在选项中开启）",
            "Something to do while dead: stake points on who dies next, or whether anyone gets out (the host turns it on)",
            () => Baseline(6, 6),
            Step(2.2f, "按键打开竞猜", "Press the key to open the bets", () => SocialTest?.Invoke(SplitScreenTestAction.OpenBets))
                .Keyed(() => Key(_config.Camera.BetKey.Value)).Zoom(() => Social(SplitScreenDemoTarget.Bets)),
            Step(2, "先选押多少分", "Pick how much to stake", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.Stake, 20)).Zoom(() => Social(SplitScreenDemoTarget.Bets)),
            Step(2.6f, "再点一位队友：押他是下一个阵亡的", "Then click a teammate: you bet they are the next to die", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.DeathOption, 0)).Zoom(() => Social(SplitScreenDemoTarget.DeathMarket)),
            Step(3.4f, "临近揭晓前 5 秒内下的注不算，原数退还", "Bets in the last 5 seconds before the answer don't count and are refunded", () => { })
                .Zoom(() => Social(SplitScreenDemoTarget.DeathMarket)),
            // The bet teammate dies here: the answer is decided a second later (deaths that close together count
            // as one), so the result card is already up when the next step turns to it.
            Step(2.6f, "积分榜：看看谁最会猜", "The leaderboard shows who reads the room best", () => _population.Kill(PreviewKey(0)))
                .Zoom(() => Social(SplitScreenDemoTarget.Leaderboard)),
            Step(2.8f, "押中了！猜对的人按押注比例分走整个奖池", "Called it! Those who guessed right split the whole pool by their stakes",
                () => SocialTest?.Invoke(SplitScreenTestAction.CloseSocial))
                .Zoom(() => Social(SplitScreenDemoTarget.BetResult)),
            Step(2.6f, "1 秒内同时阵亡的几个人，都算正确答案", "Players who die within a second of each other all count as the answer", () => { })) { Bets = true },
        new DemoChapter("同事评估", "Colleague reviews",
            "给还活着的同事写评语，回到轨道后由公司统一「表彰」", "Review your living colleagues; the Company reads it all out back in orbit",
            () => Baseline(5, 6),
            Step(2.2f, "按键打开同事评估", "Press the key to open the reviews", () => SocialTest?.Invoke(SplitScreenTestAction.OpenRatings))
                .Keyed(() => Key(_config.Camera.RateKey.Value)).Zoom(() => Social(SplitScreenDemoTarget.Reviews)),
            Step(2.2f, "上排是好评", "The top row is praise", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.ReviewTag, 0, 0)).Zoom(() => Social(SplitScreenDemoTarget.ReviewRow, 0)),
            Step(2.4f, "下排是吐槽；每位同事最多两条", "The bottom row is teasing; two per colleague", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.ReviewTag, 0, 5)).Zoom(() => Social(SplitScreenDemoTarget.ReviewRow, 0)),
            Step(2.2f, "只统计条数，不公开是谁写的", "Only the counts are shown, never who wrote them", SocialPress)
                .Press(() => Social(SplitScreenDemoTarget.ReviewTag, 1, 2)).Zoom(() => Social(SplitScreenDemoTarget.ReviewRow, 1)),
            Step(8, "回到轨道后，公司公布本日绩效通报", "Back in orbit the Company posts today's performance notice", () => SocialTest?.Invoke(SplitScreenTestAction.EndRound))
                .Zoom(() => Social(SplitScreenDemoTarget.Notice, int.MaxValue)),
            Step(1, "回到轨道后，公司公布本日绩效通报", "Back in orbit the Company posts today's performance notice",
                () => SocialTest?.Invoke(SplitScreenTestAction.CloseSocial))),
    };

    private static DemoStep Step(float seconds, string zh, string en, Action run) => new(seconds, zh, en, run);
    private string Key(KeyCode key) => SpectatorHotkeySettings.KeyLabel(key, _config.UseChineseText);
    private Rect? LargeView() => _focused.HasValue ? _view?.FocusTile(_focused.Value) : null;
    private Rect? HoveredMember() => _demoKey.HasValue ? _view?.FocusAudienceMember(_demoKey.Value) : null;
    private Rect? AudienceChip(SplitScreenKey? key) => key.HasValue ? _view?.FocusAudienceChip(key.Value) : null;
    private Rect? EmoteRoom() => _view?.FocusAudienceRoom();
    private Rect? Social(SplitScreenDemoTarget target, int a = 0, int b = 0) => SocialFocus?.Invoke(target, a, b);
    // A click where the pointer landed, through the panels' own click handling.
    private void SocialPress() => SocialClick?.Invoke(_demoPoint);
    private void Hover(SplitScreenKey? key) { _demoKey = key; _view?.SetDeadHover(key); }
    private static SplitScreenKey PreviewKey(int slot) => new(ulong.MaxValue, (ulong)slot);

    /// <summary>Plays one chapter, or every chapter when <paramref name="chapter"/> is negative.</summary>
    private void StartDemo(int chapter)
    {
        if (!Preview || _view == null) return;
        _demo.Clear();
        for (int i = 0; i < Chapters.Length; i++)
            if (chapter < 0 || chapter == i)
            {
                _demo.Add((i, null));
                foreach (var step in Chapters[i].Steps) _demo.Add((i, step));
            }
        if (_demo.Count == 0) return;
        _demoSaved = (_population.Count, _population.Audience, _previewSpeaking, _config.Camera.MonitorInfrared.Value,
            _config.Camera.SplitScreen.ThermalAllViews.Value, _preferredMode, _preferredStyle);
        _demoIndex = 0; _demoWait = 0; _demoChapter = -1;
        // A clean screen: no toolbar, no menu, no pointer. Opening the menu again (ESC) stops the demo.
        _view.SetTestVisible(false); _view.HideKeyHintsNow();
        if (_game.Read().MenuOpen) _game.TogglePreviewPointer();
        ModLog.Debug("Split-screen demo started.");
    }

    private void TickDemo(SplitScreenSessionContext context)
    {
        if (context.MenuOpen) { StopDemo(finished: false); return; }
        // A subject that is not drawn yet (a result card decided a second after the death, a panel reopening) keeps
        // the last framing without ringing it; one that still has not come 1.5 s after the step acted gives way to
        // the whole picture.
        Rect? focus = null; bool held = false;
        if (_demoFocus != null)
        {
            if (_demoFocus() is { } subject) { focus = _demoLastFocus = subject; _demoMissing = 0; }
            else
            {
                held = true;
                if (_demoPending == null && (_demoMissing += Time.unscaledDeltaTime) > 1.5f) _demoLastFocus = null;
                focus = _demoLastFocus;
            }
        }
        _view?.SetZoom(focus, held: held);
        // The step acts once the pointer and the picture have arrived; its reading time starts then.
        if (_demoPending != null)
        {
            if (_view?.DemoReady != true) return;
            var run = _demoPending; _demoPending = null; run();
        }
        _demoWait -= Time.unscaledDeltaTime;
        if (_demoWait > 0) return;
        NextDemoStep();
    }

    private void NextDemoStep()
    {
        if (_demoIndex >= _demo.Count) { StopDemo(finished: true); return; }
        var (index, step) = _demo[_demoIndex++];
        var chapter = Chapters[index];
        bool chinese = _config.UseChineseText;
        _demoChapter = index;
        if (step == null)
        {
            // The chapter's card: what the feature is for, while its scene is set up underneath.
            _demoFocus = null; _demoLastFocus = null; _demoKey = null; _view?.MoveDemoCursor(null, false); _view?.SetCaption(null, null);
            chapter.Setup();
            if (_demoCaptions) _view?.ShowDemoCard(chinese ? chapter.Zh : chapter.En, chinese ? chapter.WhatZh : chapter.WhatEn,
                chinese ? $"第 {index + 1} / {Chapters.Length} 章" : $"Chapter {index + 1} of {Chapters.Length}");
            _demoWait = CardSeconds;
            return;
        }
        _view?.HideDemoCard();
        _demoFocus = step.Focus; _demoMissing = 0;
        _demoWait = step.Seconds;
        if (_demoCaptions)
        {
            _view?.SetCaption(chinese ? chapter.Zh : chapter.En, chinese ? step.Zh : step.En, step.Key?.Invoke(),
                Array.IndexOf(chapter.Steps, step), chapter.Steps.Length);
        }
        if (step.Pointer?.Invoke() is { } target)
        {
            _demoPoint = target.center;
            _view?.MoveDemoCursor(_demoPoint, step.Click);
        }
        else _view?.MoveDemoCursor(null, false);
        _demoPending = step.Run;
    }

    private void StopDemo(bool finished)
    {
        if (_demo.Count == 0) return;
        ReleaseDemo();
        ShowPreviewPanel();
        // Back to the toolbar: a finished demo reopens the menu; ESC already did.
        if (finished && !_game.Read().MenuOpen) _game.TogglePreviewPointer();
        ModLog.Debug(finished ? "Split-screen demo finished." : "Split-screen demo stopped.");
    }

    // Puts back what the demo changed (counts, speaking, thermal, thermal in every view, the camera choice) and clears
    // what it drew. Shared by its own end and by every forced stop (scene change, mod disabled, preview closed).
    private void ReleaseDemo()
    {
        _demo.Clear(); _demoPending = null; _demoFocus = null; _demoLastFocus = null; _demoKey = null; _demoChapter = -1;
        Baseline(_demoSaved.Count, _demoSaved.Audience);
        _previewSpeaking = _demoSaved.Speaking; _config.Camera.MonitorInfrared.Value = _demoSaved.Thermal;
        _preferredMode = _demoSaved.Mode; _preferredStyle = _demoSaved.Style;
        _view?.ClearDemo();
    }

    // A known starting point for each chapter.
    private void Baseline(int count, int audience)
    {
        _population.SetCount(count); _population.SetAudience(audience); _population.Playing = false;
        StopWatchTogether(); Focus(null);
        _previewSpeaking = false; _config.Camera.MonitorInfrared.Value = false; _config.Camera.SplitScreen.ThermalAllViews.Value = _demoSaved.ThermalAll;
        _view?.SetDeadHover(null); _view?.SetViewersHover(false);
        SocialTest?.Invoke(SplitScreenTestAction.CloseSocial);
    }

    private void FocusSlot(int slot) => Focus(PreviewKey(slot));

    // A preview view showing a real living teammate (not yourself), when the game has one: view i shows source i
    // (the sources repeat when there are more views than players).
    private SplitScreenKey? TeammateView()
    {
        _game.CopyLivingPlayersTo(_demoTeammates, false);
        for (int i = 0; i < _sources.Count && i < _population.Count; i++)
            foreach (var teammate in _demoTeammates)
                if (_sources[i].Source == teammate.Source) return PreviewKey(i);
        return null;
    }

    // An audience-only identity with or without the mod (every fourth has none).
    private SplitScreenKey? AudienceKey(bool enhanced)
    {
        foreach (var entry in _dead)
            if (entry.Key.SlotId >= SplitScreenTestPopulation.AudienceFirstSlot && entry.Key.SlotId < PreviewLocalSlot && entry.Enhanced == enhanced) return entry.Key;
        return null;
    }

    // The first audience member with the mod who is watching someone: the one to watch along with.
    private SplitScreenKey? WatchableKey()
    {
        foreach (var entry in _dead)
            if (entry.Key.SlotId >= SplitScreenTestPopulation.AudienceFirstSlot && entry.Key.SlotId < PreviewLocalSlot && entry.Enhanced && entry.Watching.HasValue)
                return entry.Key;
        return null;
    }

    private void WatchAlong()
    {
        foreach (var entry in _dead)
            if (entry.Key == WatchableKey()) { ToggleWatchTogether(entry); return; }
    }
}

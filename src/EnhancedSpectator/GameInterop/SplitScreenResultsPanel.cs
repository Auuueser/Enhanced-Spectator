using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The split-screen style end-of-round report: the game's performance report (grade, collected scrap, each crew
/// member's state and notes, casualty penalty, level) over a blurred still of the last view, its parts animating in
/// as the game reaches them. Once the crew is revived (before the days-left banner), the still dissolves into the
/// live view, blurred the same way. The data comes from <see cref="RoundReport"/>; this class only draws.
/// </summary>
internal sealed partial class SplitScreenResultsPanel : IDisposable
{
    // Layout in units of a 1080-pixel-high screen (the canvas scaler fits it to the real one).
    private const float PanelWidth = 1280, PanelHeight = 720, Pad = 32, ColumnWidth = 404, CrewTop = 150, CardGap = 8,
        SectionTop = 470, SectionHeight = 104, SectionGap = 12;
    // Timeline, seconds from the game's own stats animation starting (the report opens with it). The game's
    // end-of-round music is laid against that animation (V81 EndgameStats clip): a crew member's state at 0.53,
    // 1.85, 3.15 and 4.47 s, their notes 0.45 s later, the all-dead stamp at 5.25, the scrap totals at 5.43 and the
    // grade at 6.92 s. Ours keeps the same beats so it lands on the same music.
    private static readonly float[] CrewBeats = { .533f, 1.85f, 3.15f, 4.467f };
    internal const float FadeSeconds = .2f, NotesDelay = .45f, AllDeadAt = 5.25f, CountStart = 5.433f, CountSeconds = .8f,
        GradeAt = 6.917f, CardSeconds = .38f, NoteStagger = .12f, NoteSeconds = .25f, StampSeconds = .32f, RippleSeconds = .6f,
        SectionSeconds = .4f, BumpSeconds = .067f, ShakeSeconds = .5f, CloseSeconds = .6f, ShutterSeconds = .28f;
    /// <summary>When crew card <paramref name="index"/> of <paramref name="count"/> arrives: the game's four beats,
    /// or spread across them for a larger crew.</summary>
    internal static float CrewBeat(int index, int count)
        => count <= CrewBeats.Length ? CrewBeats[index] : CrewBeats[0] + index * (CrewBeats[CrewBeats.Length - 1] - CrewBeats[0]) / (count - 1);
    private static readonly Color Text = new Color(.94f, .94f, .94f, 1), Muted = new Color(.6f, .62f, .64f, 1),
        Edge = new Color(.2f, .21f, .23f, 1), Box = new Color(.045f, .048f, .056f, 1), Accent = new Color(1, .38f, .02f, 1),
        Alive = new Color(.36f, .86f, .46f, 1), Dead = new Color(1, .27f, .24f, 1), Missing = new Color(1, .72f, .2f, 1);

    private sealed class Card
    {
        internal RectTransform Root = null!;
        internal CanvasGroup Group = null!;
        internal RawImage Avatar = null!;
        internal TextMeshProUGUI Name = null!;
        internal readonly List<RectTransform> Chips = new List<RectTransform>();
        internal Vector2 Home;
        internal string NotesText = string.Empty;
        internal ulong SteamId;
        internal float NotesFrom, NotesLeft, NotesWidth, ChipHeight, ChipFont;
    }

    private readonly Canvas _canvas;
    private readonly CanvasGroup _group, _panelGroup;
    private readonly RawImage _backdrop, _live, _vignette;
    private readonly RectTransform _panel, _crew, _gradeBox, _penaltyBox, _levelBox, _lost;
    private readonly TextMeshProUGUI _title, _subtitle, _crewLabel, _gradeLabel, _grade, _collectLabel, _collectValue, _collectRatio,
        _penaltyTitle, _penaltyLines, _penaltyDue, _levelTitle, _levelName, _levelExperience, _lostText;
    private readonly RectTransform _collectFill, _levelFill;
    private readonly List<Card> _cards = new List<Card>();
    private readonly List<string> _notes = new List<string>(3);
    private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
    private readonly Texture2D _vignetteTexture;
    private RenderTexture? _blur, _liveBlur;
    // How far the live view has come in over the still (0-1).
    private float _liveShown;
    private const float LiveSeconds = .6f;
    private static readonly Color BackdropTint = new Color(.4f, .42f, .46f, 1);
    private TMP_FontAsset? _font;
    private Material? _material;
    private bool _chinese;
    private int _collected, _total, _shownCollected = -1, _sectionsUsed;
    // Seconds since opening and since closing began (-1: not open, not closing); advanced by Tick.
    private float _elapsed = -1, _closing = -1, _penaltyAt = -1, _levelAt = -1, _levelShakeAt = -1, _levelShownFill;
    private float _penaltyTop, _levelTop;
    private Canvas? _beneath;

    internal SplitScreenResultsPanel()
    {
        _vignetteTexture = VignetteTexture();
        _canvas = new GameObject("EnhancedSpectator Round Report", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup)).GetComponent<Canvas>();
        UnityEngine.Object.DontDestroyOnLoad(_canvas.gameObject);
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 32005;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        _group = _canvas.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
        var root = _canvas.transform;

        _backdrop = Picture("Backdrop", root); SplitScreenView.Stretch(_backdrop.rectTransform);
        _live = Picture("Live backdrop", root); SplitScreenView.Stretch(_live.rectTransform); _live.enabled = false;
        _vignette = Picture("Vignette", root); SplitScreenView.Stretch(_vignette.rectTransform); _vignette.texture = _vignetteTexture; _vignette.color = Color.white;

        _panel = Rect("Report", root, Alpha(Box, .94f), PanelWidth, PanelHeight);
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, .5f); _panel.anchoredPosition = Vector2.zero;
        _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
        SplitScreenView.Frame4("Report edge ", _panel, Edge, 1);
        var accent = Rect("Report accent", _panel, Accent, PanelWidth, 3); Place(accent, 0, 0);

        _title = Label("Title", _panel, 34, Text); Place(_title.rectTransform, Pad, 22, 700, 44);
        _subtitle = Label("Planet", _panel, 16, Muted); Place(_subtitle.rectTransform, Pad, 66, 700, 22);
        var divider = Rect("Header divider", _panel, Edge, PanelWidth - 2 * Pad, 1); Place(divider, Pad, 100);

        float crewWidth = PanelWidth - 3 * Pad - ColumnWidth, column = PanelWidth - Pad - ColumnWidth;
        _crewLabel = Label("Crew label", _panel, 14, Muted); Place(_crewLabel.rectTransform, Pad, 116, crewWidth, 20);
        _crew = Rect("Crew", _panel, Color.clear, crewWidth, PanelHeight - CrewTop - Pad); Place(_crew, Pad, CrewTop);
        _lost = Rect("All crew lost", _panel, new Color(.3f, .05f, .05f, .92f), crewWidth, 40); Place(_lost, Pad, CrewTop);
        Rect("All crew lost edge", _lost, Dead, 3, 40).anchoredPosition = Vector2.zero;
        _lostText = Label("All crew lost text", _lost, 17, new Color(1, .82f, .8f, 1)); Place(_lostText.rectTransform, 16, 0, crewWidth - 24, 40);
        _lostText.alignment = TextAlignmentOptions.MidlineLeft;

        _gradeBox = Section("Grade", column, 116, 222, out _gradeLabel);
        CreateSeal();
        _grade = Label("Grade letter", _gradeBox, 128, Text); _grade.alignment = TextAlignmentOptions.Center;
        var gradeRect = _grade.rectTransform; gradeRect.anchorMin = gradeRect.anchorMax = gradeRect.pivot = new Vector2(.5f, .5f);
        gradeRect.sizeDelta = new Vector2(220, 170); gradeRect.anchoredPosition = Vector2.zero; gradeRect.localRotation = Quaternion.Euler(0, 0, -8);

        var collect = Section("Collected", column, 350, SectionHeight, out _collectLabel);
        _collectValue = Label("Collected value", collect, 30, Text); Place(_collectValue.rectTransform, 16, 30, ColumnWidth - 120, 38);
        _collectRatio = Label("Collected ratio", collect, 16, Muted); Place(_collectRatio.rectTransform, ColumnWidth - 116, 40, 100, 24);
        _collectRatio.alignment = TextAlignmentOptions.MidlineRight;
        _collectFill = Bar("Collected bar", collect, 80, Accent);

        _levelBox = Section("Level", column, SectionTop, SectionHeight, out _levelTitle);
        _levelName = Label("Level name", _levelBox, 20, Text); Place(_levelName.rectTransform, 16, 34, ColumnWidth - 150, 28);
        _levelExperience = Label("Level experience", _levelBox, 16, Muted); Place(_levelExperience.rectTransform, ColumnWidth - 146, 38, 130, 24);
        _levelExperience.alignment = TextAlignmentOptions.MidlineRight;
        _levelFill = Bar("Level bar", _levelBox, 76, new Color(.35f, .75f, 1, 1));
        CreateLevel();

        _penaltyBox = Section("Penalty", column, SectionTop, SectionHeight, out _penaltyTitle);
        Rect("Penalty edge", _penaltyBox, Dead, 3, SectionHeight).anchoredPosition = Vector2.zero;
        _penaltyTitle.color = Dead;
        _penaltyLines = Label("Penalty lines", _penaltyBox, 14, Muted); Place(_penaltyLines.rectTransform, 16, 32, ColumnWidth - 32, 40);
        _penaltyLines.enableWordWrapping = true; _penaltyLines.overflowMode = TextOverflowModes.Truncate; _penaltyLines.alignment = TextAlignmentOptions.TopLeft;
        _penaltyDue = Label("Penalty due", _penaltyBox, 22, new Color(1, .55f, .5f, 1)); Place(_penaltyDue.rectTransform, 16, 70, ColumnWidth - 32, 28);
        _penaltyDue.alignment = TextAlignmentOptions.MidlineRight;

        _canvas.gameObject.SetActive(false);
    }

    internal bool Visible => _elapsed >= 0;
    internal bool DrawsBeneathHud => Visible && _beneath != null;
    internal bool Closing => _closing >= 0;
    /// <summary>Fully over the screen: the views beneath need not draw.</summary>
    internal bool Covering => Visible && _closing < 0 && _elapsed >= FadeSeconds;
    internal Canvas Canvas => _canvas;
    /// <summary>A player's avatar by Steam id once it has arrived.</summary>
    internal Func<ulong, Texture?>? AvatarOf { get; set; }

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (_font == font) return;
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = font != null ? SpectatorTextStyle.CreateLabelMaterial(font) : null;
        foreach (var text in _texts) ApplyFont(text);
    }

    /// <summary>Opens the report over a blurred still of <paramref name="backdrop"/> (the last view drawn).</summary>
    internal void Open(RoundReport report, Texture? backdrop, bool chinese)
    {
        ClearCards();
        _chinese = chinese;
        _elapsed = 0; _closing = _penaltyAt = _levelAt = _levelShakeAt = -1;
        // A reopened report starts without the last one's level (its rank change would read as a level-up) or penalty.
        _levelName.text = _levelExperience.text = _penaltyLines.text = _penaltyDue.text = string.Empty; _sectionsUsed = 0; _shownCollected = -1; _levelShownFill = 0;
        OpenLevel();
        // A scene-unload handoff may already have copied the last frame before the renderer released it.
        if (backdrop != null || _blur == null) CaptureBackdrop(backdrop);
        _title.text = chinese ? "绩效报告" : "PERFORMANCE REPORT";
        _subtitle.text = report.Planet;
        _crewLabel.text = chinese ? $"船员 · {report.Players.Count}" : $"CREW · {report.Players.Count}";
        _gradeLabel.text = chinese ? "评级" : "GRADE";
        _collectLabel.text = chinese ? "回收废品" : "SCRAP COLLECTED";
        _levelTitle.text = chinese ? "等级" : "LEVEL";
        _penaltyTitle.text = chinese ? "伤亡罚款" : "CASUALTY PENALTY";
        _lostText.text = chinese ? "全员阵亡 · 本日回收的废品全部丢失" : "ALL CREW LOST · today's scrap is lost";
        _grade.text = report.Grade; _grade.color = GradeColor(report.Grade);
        OpenSeal(GradeColor(report.Grade));
        _collected = report.Collected; _total = report.Total;
        _lost.gameObject.SetActive(report.AllDead);
        BuildCards(report);
        _penaltyBox.gameObject.SetActive(false); _levelBox.gameObject.SetActive(false);
        _canvas.gameObject.SetActive(true);
        Animate(report, 0);
    }

    /// <summary>Starts the closing fade; the report hides once it ends.</summary>
    internal void Close() { if (Visible && _closing < 0) _closing = 0; }

    /// <summary>
    /// Draws beneath the game HUD canvas while its menu or chat is open (null: above everything).
    /// </summary>
    internal void SetBeneath(Canvas? hud)
    {
        if (_beneath == hud) return;
        _beneath = hud;
        SplitScreenView.Layer(_canvas, hud, 32005, -1);
    }

    internal void Tick(RoundReport report, float deltaTime)
    {
        if (!Visible) return;
        _elapsed += deltaTime;
        if (_closing >= 0 && (_closing += deltaTime) >= CloseSeconds) { Hide(); return; }
        Animate(report, deltaTime);
    }

    private void Animate(RoundReport report, float deltaTime)
    {
        float t = _elapsed;
        // Closing hands over to the game's days-left banner, which opens out of a thin line at the screen centre with
        // its sound: the panel folds shut into a line the same way, while the blurred still dissolves into the live
        // view (the crew is alive again by then) a little behind it.
        float closing = _closing >= 0 ? Mathf.Clamp01(_closing / CloseSeconds) : 0;
        float shutter = _closing >= 0 ? Mathf.Clamp01(_closing / ShutterSeconds) : 0;
        float open = EaseOut(t / FadeSeconds), scale = Mathf.Lerp(1.06f, 1, open);
        _group.alpha = open * (1 - Mathf.SmoothStep(0, 1, closing));
        _panelGroup.alpha = 1 - EaseIn(shutter);
        _panel.localScale = new Vector3(scale * (1 + .04f * EaseOut(shutter)), scale * Mathf.Lerp(1, .02f, EaseIn(shutter)), 1);

        // Collected scrap counts up and its bar fills, when the game shows its totals.
        float count = EaseOut((t - CountStart) / CountSeconds);
        int collected = Mathf.RoundToInt(_collected * count);
        if (collected != _shownCollected)
        {
            _shownCollected = collected;
            _collectValue.text = "$" + Number(collected) + " / $" + Number(_total);
            _collectRatio.text = _total > 0 ? Mathf.RoundToInt(100f * collected / _total) + "%" : "—";
        }
        _collectFill.anchorMax = new Vector2(_total > 0 ? Mathf.Clamp01((float)collected / _total) : 0, 1);

        // Crew cards rise in on the game's beats; their notes pop in after them.
        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            float start = CrewBeat(i, _cards.Count), p = Mathf.Clamp01((t - start) / CardSeconds);
            card.Group.alpha = EaseOut(p);
            card.Root.anchoredPosition = card.Home - new Vector2(0, (1 - EaseOutBack(p)) * 18);
            if (i < report.Players.Count && report.Players[i].NotesText != card.NotesText) BuildNotes(card, report.Players[i].NotesText);
            if (card.Avatar.texture == null && card.SteamId != 0 && AvatarOf != null)
            {
                card.Avatar.texture = AvatarOf(card.SteamId);
                card.Avatar.color = card.Avatar.texture != null ? Color.white : new Color(.12f, .13f, .15f, 1);
            }
            for (int j = 0; j < card.Chips.Count; j++)
            {
                float q = Mathf.Clamp01((t - start - NotesDelay - j * NoteStagger) / NoteSeconds);
                card.Chips[j].localScale = Vector3.one * Mathf.Lerp(.6f, 1, EaseOutBack(q));
                card.Chips[j].GetComponent<CanvasGroup>().alpha = EaseOut(q);
            }
        }

        // The grade is stamped on its beat into its seal, which draws itself round just before.
        float stamp = Mathf.Clamp01((t - GradeAt) / StampSeconds);
        _grade.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1, EaseOut(stamp));
        _grade.alpha = stamp;
        AnimateSeal(t);
        if (_lost.gameObject.activeSelf)
        {
            float lost = EaseOut((t - AllDeadAt) / SectionSeconds);
            _lost.localScale = new Vector3(lost, 1, 1);
        }

        // Level and penalty come in when the game reaches them (its experience and level sounds start then), each
        // into the next free place: the level slides in and shakes when the rank changes, the penalty lands with the
        // game's bump and wobble.
        if (report.Level && _levelAt < 0) { _levelAt = t; _levelTop = NextSection(); _levelBox.gameObject.SetActive(true); }
        if (report.Penalty && _penaltyAt < 0) { _penaltyAt = t; _penaltyTop = NextSection(); _penaltyBox.gameObject.SetActive(true); }
        if (_levelAt >= 0) AnimateLevel(report, t, deltaTime);
        if (_penaltyAt >= 0)
        {
            Land(_penaltyBox, _penaltyTop, t - _penaltyAt);
            if (_penaltyLines.text != report.PenaltyLines) _penaltyLines.text = report.PenaltyLines;
            if (_penaltyDue.text != report.PenaltyDue) _penaltyDue.text = report.PenaltyDue;
        }
    }

    private float NextSection() => SectionTop + _sectionsUsed++ * (SectionHeight + SectionGap);

    // The right-column boxes turn and scale about their centre.
    private static Vector2 SectionCentre(float top) => new Vector2(PanelWidth - Pad - ColumnWidth / 2, -top - SectionHeight / 2);
    private static void Slide(RectTransform box, float top, float elapsed, float shake)
    {
        float p = EaseOut(elapsed / SectionSeconds);
        box.anchoredPosition = SectionCentre(top) + new Vector2((1 - p) * 40, 0);
        box.localRotation = Quaternion.Euler(0, 0, Wobble(shake, 4));
        box.GetComponent<CanvasGroup>().alpha = p;
    }
    private static void Land(RectTransform box, float top, float elapsed)
    {
        box.anchoredPosition = SectionCentre(top);
        box.localScale = Vector3.one * (elapsed < BumpSeconds ? Mathf.Lerp(1.5f, 1, elapsed / BumpSeconds) : 1);
        box.localRotation = Quaternion.Euler(0, 0, Wobble(elapsed, 7));
        box.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(elapsed / BumpSeconds);
    }
    // A decaying shake (degrees) for half a second after it starts; none before or after.
    internal static float Wobble(float elapsed, float degrees)
        => elapsed < 0 || elapsed >= ShakeSeconds ? 0 : degrees * Mathf.Sin(elapsed * 42) * (1 - elapsed / ShakeSeconds);

    private void BuildCards(RoundReport report)
    {
        int count = report.Players.Count;
        float width = _crew.sizeDelta.x, height = _crew.sizeDelta.y - (report.AllDead ? 48 : 0), top = report.AllDead ? 48 : 0;
        int columns = count <= 6 ? 1 : count <= 14 ? 2 : 3, rows = Math.Max(1, (count + columns - 1) / columns);
        float cardWidth = (width - (columns - 1) * CardGap) / columns;
        float cardHeight = Mathf.Min(92, (height - (rows - 1) * CardGap) / rows);
        bool twoLines = cardHeight >= 56;
        for (int i = 0; i < count; i++)
        {
            var player = report.Players[i];
            int column = i / rows, row = i % rows;
            var root = Rect("Crew " + i, _crew, new Color(.06f, .065f, .075f, .96f), cardWidth, cardHeight);
            var card = new Card { Root = root, Group = root.gameObject.AddComponent<CanvasGroup>(), SteamId = player.SteamId };
            card.Home = new Vector2(column * (cardWidth + CardGap), -(top + row * (cardHeight + CardGap)));
            root.anchoredPosition = card.Home;
            var state = StateColor(player.State);
            Rect("State edge", root, state, 3, cardHeight).anchoredPosition = Vector2.zero;
            float avatar = Mathf.Max(20, cardHeight - 16);
            var frame = Rect("Avatar frame", root, twoLines ? Edge : Alpha(state, .8f), avatar + 2, avatar + 2); Place(frame, 13, (cardHeight - avatar) / 2 - 1);
            card.Avatar = Picture("Avatar", frame); SplitScreenView.Stretch(card.Avatar.rectTransform);
            card.Avatar.rectTransform.offsetMin = Vector2.one; card.Avatar.rectTransform.offsetMax = -Vector2.one;
            card.Avatar.color = new Color(.12f, .13f, .15f, 1);
            float left = 14 + avatar + 12, nameRight = cardWidth - 12;
            // Two lines: a state chip at the top right. One line (a large crew): the edge and the avatar frame carry
            // the state, leaving the width to the notes.
            if (twoLines)
            {
                var chip = Rect("State", root, Alpha(state, .16f), 10, 20);
                var chipText = Label("State text", chip, 12, state, false); SplitScreenView.Stretch(chipText.rectTransform);
                chipText.alignment = TextAlignmentOptions.Center; chipText.text = StateText(player.State);
                float chipWidth = chipText.GetPreferredValues(chipText.text).x + 16;
                chip.sizeDelta = new Vector2(chipWidth, 20);
                chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1, 1);
                chip.anchoredPosition = new Vector2(-10, -9);
                nameRight = cardWidth - chipWidth - 22;
            }
            card.Name = Label("Name", root, twoLines ? 18 : 15, player.Local ? Accent : Text, false);
            card.Name.text = player.Local ? player.Name + (_chinese ? "（你）" : " (you)") : player.Name;
            float nameWidth = twoLines ? nameRight - left : Mathf.Min(card.Name.GetPreferredValues(card.Name.text).x + 2, (nameRight - left) * .42f);
            Place(card.Name.rectTransform, left, twoLines ? 8 : 0, nameWidth, twoLines ? 26 : cardHeight);
            card.Name.alignment = twoLines ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            card.ChipHeight = twoLines ? 22 : 18; card.ChipFont = twoLines ? 13 : 11;
            card.NotesLeft = twoLines ? left : left + nameWidth + 10;
            card.NotesWidth = (twoLines ? cardWidth - 12 : nameRight) - card.NotesLeft;
            card.NotesFrom = twoLines ? cardHeight - 8 - card.ChipHeight : (cardHeight - card.ChipHeight) / 2;
            card.NotesText = "\u0000";
            _cards.Add(card);
        }
    }

    // The player's notes as chips in a row, as many as fit.
    private void BuildNotes(Card card, string text)
    {
        card.NotesText = text;
        foreach (var chip in card.Chips) Discard(chip);
        card.Chips.Clear();
        RoundReport.ParseNotes(text, _notes);
        float x = card.NotesLeft;
        foreach (var note in _notes)
        {
            var chip = Rect("Note", card.Root, new Color(.22f, .1f, .02f, .95f), 10, card.ChipHeight);
            chip.gameObject.AddComponent<CanvasGroup>();
            SplitScreenView.Frame4("Note edge ", chip, Alpha(Accent, .55f), 1);
            var label = Label("Note text", chip, card.ChipFont, new Color(1, .8f, .62f, 1), false); SplitScreenView.Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center; label.text = note;
            float width = label.GetPreferredValues(note).x + 16;
            if (x + width > card.NotesLeft + card.NotesWidth) { Discard(chip); break; }
            chip.pivot = new Vector2(.5f, .5f);
            chip.sizeDelta = new Vector2(width, card.ChipHeight);
            chip.anchoredPosition = new Vector2(x + width / 2, -card.NotesFrom - card.ChipHeight / 2);
            card.Chips.Add(chip);
            x += width + 6;
        }
    }

    private string StateText(RoundReportState state) => state switch
    {
        RoundReportState.Deceased => _chinese ? "死亡" : "DECEASED",
        RoundReportState.Missing => _chinese ? "失踪" : "MISSING",
        _ => _chinese ? "存活" : "ALIVE"
    };
    private static Color StateColor(RoundReportState state)
        => state == RoundReportState.Deceased ? Dead : state == RoundReportState.Missing ? Missing : Alive;
    internal static Color GradeColor(string grade) => grade switch
    {
        "S" => new Color(1, .8f, .25f, 1),
        "A" => new Color(.4f, .9f, .5f, 1),
        "B" => new Color(.35f, .75f, 1, 1),
        "D" => new Color(1, .58f, .22f, 1),
        "F" => Dead,
        _ => new Color(.85f, .86f, .88f, 1)
    };
    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    // A still of the last view, blurred.
    internal void CaptureBackdrop(Texture? source)
    {
        ReleaseBackdrop();
        if (source == null) { _backdrop.texture = null; _backdrop.color = new Color(.02f, .022f, .026f, 1); return; }
        _blur = BlurTarget(source, "EnhancedSpectator Report Backdrop");
        Blur(source, _blur);
        _backdrop.texture = _blur; _backdrop.color = BackdropTint;
    }

    /// <summary>
    /// The crew revived while the report shows: the still dissolves into <paramref name="source"/> (the game's live
    /// view), blurred alike every frame; null leaves the still.
    /// </summary>
    internal void SetLive(Texture? source, float deltaTime)
    {
        if (source != null && Visible)
        {
            if (_liveBlur == null || _liveBlur.width != Math.Max(8, source.width / 4) || _liveBlur.height != Math.Max(8, source.height / 4))
            { ReleaseLive(); _liveBlur = BlurTarget(source, "EnhancedSpectator Report Live Backdrop"); }
            Blur(source, _liveBlur);
            _live.texture = _liveBlur;
        }
        _liveShown = Mathf.MoveTowards(_liveShown, source != null && _liveBlur != null ? 1 : 0, deltaTime / LiveSeconds);
        _live.enabled = _liveShown > 0;
        var tint = BackdropTint; tint.a = Mathf.SmoothStep(0, 1, _liveShown); _live.color = tint;
    }

    private static RenderTexture BlurTarget(Texture source, string name)
    {
        var target = new RenderTexture(Math.Max(8, source.width / 4), Math.Max(8, source.height / 4), 0)
        { name = name, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        target.Create();
        return target;
    }

    // A soft blur with the stock blit alone: each halving with bilinear filtering averages 2x2 pixels, and the way back
    // up doubles step by step, so the picture reads as blurred rather than as a shrunken copy stretched back to size.
    // The target is a quarter of the source; the chain goes on down to a 32nd and back up to it.
    private static readonly RenderTexture?[] Chain = new RenderTexture?[3];
    private static void Blur(Texture source, RenderTexture target)
    {
        var active = RenderTexture.active;
        var half = RenderTexture.GetTemporary(Math.Max(8, source.width / 2), Math.Max(8, source.height / 2), 0);
        half.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, half);
        Graphics.Blit(half, target);
        Texture last = target;
        for (int i = 0; i < Chain.Length; i++)
        {
            var step = RenderTexture.GetTemporary(Math.Max(2, target.width >> (i + 1)), Math.Max(2, target.height >> (i + 1)), 0);
            step.filterMode = FilterMode.Bilinear;
            Graphics.Blit(last, step); Chain[i] = step; last = step;
        }
        for (int i = Chain.Length - 2; i >= 0; i--) { Graphics.Blit(last, Chain[i]); last = Chain[i]!; }
        Graphics.Blit(last, target);
        for (int i = 0; i < Chain.Length; i++) { RenderTexture.ReleaseTemporary(Chain[i]); Chain[i] = null; }
        RenderTexture.ReleaseTemporary(half);
        RenderTexture.active = active;
    }

    private void ReleaseBackdrop()
    {
        if (_blur == null) return;
        _blur.Release(); UnityEngine.Object.Destroy(_blur); _blur = null;
    }

    private void ReleaseLive()
    {
        _liveShown = 0; _live.enabled = false; _live.texture = null;
        if (_liveBlur == null) return;
        _liveBlur.Release(); UnityEngine.Object.Destroy(_liveBlur); _liveBlur = null;
    }

    private void ClearCards()
    {
        foreach (var card in _cards) Discard(card.Root);
        _cards.Clear();
    }
    // Destroying waits for the end of the frame; leaving the report at once keeps a reopened one clean.
    private static void Discard(Transform part) { part.SetParent(null, false); UnityEngine.Object.Destroy(part.gameObject); }

    internal void Hide()
    {
        _elapsed = _closing = -1;
        _canvas.gameObject.SetActive(false);
        ClearCards(); ReleaseBackdrop(); ReleaseLive(); _backdrop.texture = null;
    }

    public void Dispose()
    {
        Hide();
        if (_material != null) UnityEngine.Object.Destroy(_material);
        UnityEngine.Object.Destroy(_canvas.gameObject);
        DisposeSeal(); UnityEngine.Object.Destroy(_vignetteTexture);
    }

    private RectTransform Section(string name, float x, float top, float height, out TextMeshProUGUI title)
    {
        var box = Rect(name, _panel, Box, ColumnWidth, height); Place(box, x, top);
        box.pivot = new Vector2(.5f, .5f); box.anchoredPosition = new Vector2(x + ColumnWidth / 2, -top - height / 2);
        box.gameObject.AddComponent<CanvasGroup>();
        SplitScreenView.Frame4(name + " edge ", box, Edge, 1);
        title = Label(name + " title", box, 14, Muted); Place(title.rectTransform, 16, 10, ColumnWidth - 32, 20);
        return box;
    }

    private RectTransform Bar(string name, RectTransform parent, float top, Color color)
    {
        var track = Rect(name, parent, new Color(1, 1, 1, .07f), ColumnWidth - 32, 8); Place(track, 16, top);
        var fill = Rect(name + " fill", track, color, 0, 0);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
        return fill;
    }

    private static RectTransform Rect(string name, Transform parent, Color color, float width, float height)
    {
        var image = SplitScreenView.CreateImage(name, parent, color);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static void Place(RectTransform rect, float x, float top, float width = -1, float height = -1)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -top);
        if (width >= 0) rect.sizeDelta = new Vector2(width, height);
    }
    private static RawImage Picture(string name, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false); image.raycastTarget = false; return image;
    }
    // Fixed labels are kept for a later font change; crew card labels live only as long as their report.
    private TextMeshProUGUI Label(string name, Transform parent, float size, Color color, bool fixedLabel = true)
    {
        var text = SplitScreenView.CreateText(name, parent);
        text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(text);
        if (fixedLabel) _texts.Add(text);
        return text;
    }
    private void ApplyFont(TextMeshProUGUI text)
    {
        if (_font == null) return;
        text.font = _font;
        if (_material != null) text.fontSharedMaterial = _material;
    }

    private static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
    internal static float EaseOut(float p) { p = Mathf.Clamp01(p); return 1 - (1 - p) * (1 - p) * (1 - p); }
    internal static float EaseIn(float p) { p = Mathf.Clamp01(p); return p * p * p; }
    internal static float EaseOutBack(float p)
    {
        p = Mathf.Clamp01(p);
        const float c = 1.4f;
        return 1 + (c + 1) * Mathf.Pow(p - 1, 3) + c * Mathf.Pow(p - 1, 2);
    }

    private static Texture2D VignetteTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "EnhancedSpectator Report Vignette", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)(190 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1.3f, d))));
            }
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return texture;
    }
}

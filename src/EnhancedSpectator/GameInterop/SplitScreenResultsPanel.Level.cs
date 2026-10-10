using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The report's level box: the rank in large type, the experience bar with ticks and a bright leading edge, the
/// round's gain or loss ("+20 EXP") above it (the bar turning red as it falls), and a rank change made an event: the box flashes, its edge glows, the
/// old rank slides out as the new one slides in, and a stamp lands ("升级！" in orange, "降级" in red).
/// </summary>
internal sealed partial class SplitScreenResultsPanel
{
    private const float LevelChangeSeconds = .35f, LevelFlashSeconds = .4f, LevelGlowSeconds = .8f, LevelStampSeconds = .32f,
        LevelDeltaSeconds = .5f, LevelNameTop = 30, LevelBarTop = 74;
    private static readonly Color LevelUpColor = new Color(1, .45f, .12f, 1), LevelDownColor = new Color(1, .27f, .24f, 1),
        LevelBarColor = new Color(.35f, .75f, 1, 1);
    private TextMeshProUGUI _levelNameOld = null!, _levelDelta = null!, _levelStamp = null!;
    private Image _levelFlash = null!, _levelFillImage = null!, _levelLead = null!;
    private readonly Image[] _levelEdges = new Image[4];
    private int _levelStartXp, _levelXp, _levelDirection, _levelTrend;
    // The game's ranks (V81 HUDManager.playerLevels, lowest first) as the mod names them in Chinese. A rank another
    // mod has already translated shows as it is.
    private static readonly (string English, string Chinese)[] Ranks =
        { ("Intern", "实习生"), ("Part-timer", "兼职员工"), ("Employee", "正式员工"), ("Leader", "领队"), ("Boss", "领导") };
    private string RankText(string name)
    {
        if (_chinese) foreach (var (english, chinese) in Ranks) if (name == english) return chinese;
        return name;
    }
    private float _levelChangeAt, _levelDeltaAt;

    private void CreateLevel()
    {
        _levelName.fontSize = 22; Place(_levelName.rectTransform, 16, LevelNameTop, ColumnWidth - 150, 30);
        _levelNameOld = Label("Level previous name", _levelBox, 22, Text); Place(_levelNameOld.rectTransform, 16, LevelNameTop, ColumnWidth - 150, 30);
        _levelDelta = Label("Level gain", _levelBox, 15, LevelUpColor); Place(_levelDelta.rectTransform, ColumnWidth - 196, 10, 180, 20);
        _levelDelta.alignment = TextAlignmentOptions.MidlineRight;
        // The bar: thicker, in tenths, with a bright edge where it is filling.
        var track = (RectTransform)_levelFill.parent;
        Place(track, 16, LevelBarTop, ColumnWidth - 32, 10);
        for (int i = 1; i < 10; i++) Place(Rect("Level tick " + i, track, new Color(0, 0, 0, .5f), 1, 10), (ColumnWidth - 32) * i / 10f, 0);
        _levelFillImage = _levelFill.GetComponent<Image>();
        _levelLead = Rect("Level bar lead", _levelFill, Color.white, 3, 14).GetComponent<Image>();
        var lead = _levelLead.rectTransform; lead.anchorMin = lead.anchorMax = new Vector2(1, .5f); lead.pivot = new Vector2(.5f, .5f); lead.anchoredPosition = Vector2.zero;
        _levelFlash = Rect("Level flash", _levelBox, new Color(1, 1, 1, 0), ColumnWidth, SectionHeight).GetComponent<Image>();
        _levelStamp = Label("Level stamp", _levelBox, 30, LevelUpColor); _levelStamp.alignment = TextAlignmentOptions.Center;
        var stamp = _levelStamp.rectTransform; stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(.5f, .5f);
        stamp.sizeDelta = new Vector2(200, 44); stamp.anchoredPosition = new Vector2(ColumnWidth * .2f, 4); stamp.localRotation = Quaternion.Euler(0, 0, -8);
        for (int i = 0; i < 4; i++) _levelEdges[i] = _levelBox.Find("Level edge " + i).GetComponent<Image>();
    }

    private void OpenLevel()
    {
        _levelStartXp = _levelXp = int.MinValue; _levelChangeAt = _levelDeltaAt = -1; _levelDirection = 0; _levelTrend = 1;
        _levelNameOld.text = _levelDelta.text = _levelStamp.text = string.Empty;
        _levelName.alpha = 1; _levelName.rectTransform.anchoredPosition = new Vector2(16, -LevelNameTop);
        _levelNameOld.alpha = 0;
        _levelStamp.alpha = 0; _levelFlash.color = new Color(1, 1, 1, 0); _levelFillImage.color = LevelBarColor;
        foreach (var edge in _levelEdges) edge.color = Edge;
    }

    private void AnimateLevel(RoundReport report, float t, float deltaTime)
    {
        Slide(_levelBox, _levelTop, t - _levelAt, _levelShakeAt >= 0 ? t - _levelShakeAt : -1);
        // The round's gain or loss, read off the game's experience counter ("120 EXP").
        int xp = LeadingNumber(report.LevelExperience);
        if (xp != int.MinValue)
        {
            if (_levelStartXp == int.MinValue) _levelStartXp = xp;
            else if (xp != _levelXp) { if (_levelDeltaAt < 0) _levelDeltaAt = t; _levelTrend = xp > _levelXp ? 1 : -1; }
            _levelXp = xp;
        }
        int delta = _levelStartXp == int.MinValue || _levelXp == int.MinValue ? 0 : _levelXp - _levelStartXp;
        string rank = RankText(report.LevelName);
        if (_levelName.text != rank)
        {
            // A rank change while the bar runs is the game's level-up (or down) sound and box shake.
            if (_levelName.text.Length > 0 && t - _levelAt > SectionSeconds)
            {
                _levelNameOld.text = _levelName.text; _levelChangeAt = _levelShakeAt = t; _levelDirection = _levelTrend;
                _levelStamp.text = _levelDirection > 0 ? _chinese ? "升级！" : "LEVEL UP" : _chinese ? "降级" : "LEVEL DOWN";
                _levelStamp.color = _levelDirection > 0 ? LevelUpColor : LevelDownColor;
            }
            _levelName.text = rank;
        }
        if (_levelExperience.text != report.LevelExperience) _levelExperience.text = report.LevelExperience;
        string gain = delta == 0 ? string.Empty : delta > 0 ? $"+{delta} EXP" : $"{delta} EXP";
        if (_levelDelta.text != gain) { _levelDelta.text = gain; _levelDelta.color = delta < 0 ? LevelDownColor : LevelUpColor; }
        float rise = _levelDeltaAt < 0 ? 0 : EaseOut((t - _levelDeltaAt) / LevelDeltaSeconds);
        _levelDelta.alpha = rise; _levelDelta.rectTransform.anchoredPosition = new Vector2(ColumnWidth - 196, -10 - (1 - rise) * 8);

        float target = Mathf.Clamp01(report.LevelFill);
        bool moving = Mathf.Abs(target - _levelShownFill) > .002f;
        _levelShownFill = Mathf.Lerp(_levelShownFill, target, 1 - Mathf.Exp(-12 * deltaTime));
        _levelFill.anchorMax = new Vector2(_levelShownFill, 1);
        // A falling bar turns red.
        _levelFillImage.color = _levelTrend < 0 ? Color.Lerp(LevelBarColor, LevelDownColor, .65f) : LevelBarColor;
        _levelLead.color = new Color(1, 1, 1, moving ? .9f : .35f);

        if (_levelChangeAt < 0) return;
        float since = t - _levelChangeAt, slide = EaseOut(since / LevelChangeSeconds), dir = _levelDirection;
        // Up: the old rank rises away and the new one rises into place; down the other way.
        _levelNameOld.rectTransform.anchoredPosition = new Vector2(16, -LevelNameTop + dir * 18 * slide);
        _levelNameOld.alpha = 1 - slide;
        _levelName.rectTransform.anchoredPosition = new Vector2(16, -LevelNameTop - dir * 18 * (1 - slide));
        _levelName.alpha = slide;
        _levelFlash.color = new Color(1, 1, 1, .35f * Mathf.Clamp01(1 - since / LevelFlashSeconds));
        var glow = dir > 0 ? LevelUpColor : LevelDownColor;
        float glowing = Mathf.Clamp01(1 - since / LevelGlowSeconds);
        foreach (var edge in _levelEdges) edge.color = Color.Lerp(Edge, glow, glowing);
        float stamp = Mathf.Clamp01((since - .1f) / LevelStampSeconds);
        _levelStamp.alpha = stamp;
        _levelStamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1, EaseOut(stamp));
    }

    private static int LeadingNumber(string text)
    {
        int value = 0, digits = 0;
        foreach (char c in text)
        {
            if (c >= '0' && c <= '9') { value = value * 10 + (c - '0'); digits++; }
            else if (digits > 0 || c != ' ') break;
        }
        return digits > 0 ? value : int.MinValue;
    }
}

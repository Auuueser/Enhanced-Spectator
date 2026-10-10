using System;
using System.Collections.Generic;
using EnhancedSpectator.Features.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The audience betting panel (markets as cards, stake chips, leaderboard) and the result card that flips in when a
/// market is decided, counting the payout up.
/// </summary>
internal sealed class SpectatorBettingPanel : IDisposable
{
    internal enum Hit { None, Stake, Option, OptOut }
    private static readonly Color PanelColor = new Color(.03f, .032f, .037f, .95f), CardColor = new Color(.07f, .075f, .085f, 1),
        OptionColor = new Color(.12f, .115f, .11f, 1), Selected = new Color(.6f, .27f, .07f, 1), Winner = new Color(.18f, .42f, .2f, 1),
        Dim = new Color(.08f, .08f, .085f, 1), Accent = new Color(.96f, .55f, .2f, 1), TextColor = new Color(.9f, .88f, .84f, 1),
        Muted = new Color(.6f, .62f, .64f, 1), Good = new Color(.55f, .95f, .6f, 1), Bad = new Color(.95f, .5f, .45f, 1);
    private readonly Canvas _canvas;
    private readonly RectTransform _zoom, _panel, _result;
    // Laid out by the last render, for the demo to point at: the open "who dies next" card, its options, the leaderboard.
    // (Not simply the first card: the survival market opens after it in a round, so it is newer and listed first.)
    private RectTransform? _deathCard, _board;
    private readonly List<RectTransform> _deathOptions = new List<RectTransform>();
    private readonly CanvasGroup _panelGroup, _resultGroup;
    private readonly TextMeshProUGUI _resultTitle, _resultAnswer, _resultDelta;
    private readonly List<Image> _images = new List<Image>();
    private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
    private readonly List<(RectTransform Rect, Hit Kind, int Market, int Value)> _buttons = new List<(RectTransform, Hit, int, int)>();
    private readonly List<Color> _buttonColors = new List<Color>();
    private readonly RectTransform _toast;
    // Markets and the leaderboard scroll under the fixed header; Fill and Text draw into the current layer.
    private readonly SpectatorScrollArea _scroll;
    private Transform _layer;
    private readonly TextMeshProUGUI _toastText;
    private readonly CanvasGroup _toastGroup;
    private int _hovered = -1;
    private bool _renderedCursor;
    private float _toastAge = 99, _confirmUntil;
    private readonly Queue<(string Title, string Answer, int Delta, bool Bet, bool Notice, string? Footer)> _results = new Queue<(string, string, int, bool, bool, string?)>();
    private TMP_FontAsset? _font;
    private Material? _material;
    private int _usedImages, _usedTexts, _renderedRevision = -1, _stake = 10, _deltaShown;
    // A card shows 4.5 s, or 2.5 s while others wait (deaths in quick succession), fixed when it appears.
    private float _open, _resultAge = 99, _resultSeconds = 4.5f, _ui = 1;
    private (string Title, string Answer, int Delta, bool Bet, bool Notice, string? Footer) _current;
    internal bool IsOpen { get; private set; }
    internal Canvas Canvas => _canvas;
    internal int Stake => _stake;

    internal SpectatorBettingPanel()
    {
        _canvas = SplitScreenView.CreateCanvas("EnhancedSpectator Betting", 32007);
        _zoom = SpectatorDemoZoom.CreateRoot(_canvas.transform);
        _panel = SplitScreenView.CreateImage("Betting panel", _zoom, PanelColor).rectTransform;
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(.5f, .5f);
        SplitScreenView.Frame4("Betting edge ", _panel, new Color(Accent.r, Accent.g, Accent.b, .45f), 1);
        _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();
        _result = SplitScreenView.CreateImage("Betting result", _zoom, PanelColor).rectTransform;
        _result.anchorMin = _result.anchorMax = _result.pivot = new Vector2(.5f, 1);
        SplitScreenView.Frame4("Betting result edge ", _result, Accent, 2);
        _resultGroup = _result.gameObject.AddComponent<CanvasGroup>();
        _resultTitle = Label(_result, "Result title"); _resultAnswer = Label(_result, "Result answer"); _resultDelta = Label(_result, "Result points");
        foreach (var text in new[] { _resultTitle, _resultAnswer, _resultDelta }) text.alignment = TextAlignmentOptions.Center;
        _toast = SplitScreenView.CreateImage("Betting toast", _panel, new Color(.16f, .09f, .04f, .96f)).rectTransform;
        _toast.anchorMin = _toast.anchorMax = _toast.pivot = new Vector2(.5f, 0);
        _toastText = Label(_toast, "Betting toast text"); _toastText.alignment = TextAlignmentOptions.Center; SplitScreenView.Stretch(_toastText.rectTransform);
        _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>(); _toast.gameObject.SetActive(false);
        _scroll = new SpectatorScrollArea(_panel, "Betting scroll", new Color(Accent.r, Accent.g, Accent.b, .6f));
        _layer = _panel;
        _panel.gameObject.SetActive(false); _result.gameObject.SetActive(false);
    }

    /// <summary>Mouse wheel over the open panel scrolls its markets and leaderboard.</summary>
    internal void Scroll(Vector2 point, float notches) { if (Contains(point)) _scroll.Scroll(notches); }

    /// <summary>The first opt-out click arms it; a second one within three seconds confirms.</summary>
    internal bool ConfirmingOptOut => Time.unscaledTime < _confirmUntil;
    internal void ArmOptOut() { _confirmUntil = Time.unscaledTime + 3; _renderedRevision = -1; }

    /// <summary>A short confirmation under the panel, e.g. after a bet was placed.</summary>
    internal void Toast(string text)
    {
        _toastText.text = text; _toastText.fontSize = Mathf.Round(13 * _ui);
        float width = Mathf.Ceil(_toastText.GetPreferredValues(text, 4096, 0).x) + Mathf.Round(28 * _ui);
        _toast.sizeDelta = new Vector2(width, Mathf.Round(28 * _ui)); _toastAge = 0;
        _toast.gameObject.SetActive(true); _toast.SetAsLastSibling();
    }

    /// <summary>Screen height taken at the top (the preview toolbar); the panel and result cards stay below it.</summary>
    internal float TopInset { get; set; }
    /// <summary>A result-style card carrying only a message (for players who are not betting).</summary>
    internal void ShowNotice(string title, string detail) => _results.Enqueue((title, detail, 0, false, true, null));

    internal void ConfigureFont(TMP_FontAsset? font)
    {
        if (font == null || font == _font) return;
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _font = font; _material = SpectatorTextStyle.CreateLabelMaterial(font);
        foreach (var text in _canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) Apply(text);
    }

    /// <summary>Drops the shown and waiting result cards, the toast and an armed opt-out.</summary>
    internal void ClearResults()
    {
        _results.Clear(); _resultAge = _resultSeconds; _result.gameObject.SetActive(false);
        _toast.gameObject.SetActive(false); _toastAge = 99; _confirmUntil = 0;
    }

    internal void Toggle() { IsOpen = !IsOpen; _renderedRevision = -1; if (IsOpen) _scroll.Reset(); }
    internal void Close() => IsOpen = false;
    internal void SelectStake(int stake) { _stake = stake; _renderedRevision = -1; }

    internal Hit HitTest(Vector2 point, out int market, out int value)
    {
        market = value = 0;
        if (!IsOpen) return Hit.None;
        foreach (var button in _buttons)
            if (button.Rect.gameObject.activeInHierarchy && Reachable(button.Rect, point) && RectTransformUtility.RectangleContainsScreenPoint(button.Rect, point, Eye))
            { market = button.Market; value = button.Value; return button.Kind; }
        return Hit.None;
    }
    internal bool Contains(Vector2 point) => IsOpen && RectTransformUtility.RectangleContainsScreenPoint(_panel, point, Eye);
    private Camera? Eye => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
    // The pixel size the canvas covers: the screen, or its camera's target when drawn through a camera.
    private Vector2 Area => Eye is { } eye ? new Vector2(eye.pixelWidth, eye.pixelHeight) : new Vector2(Screen.width, Screen.height);
    // A button scrolled out of the visible window cannot be pressed through the header or below the panel.
    private bool Reachable(RectTransform button, Vector2 point) => button.parent != _scroll.Content || _scroll.Contains(point, Eye);

    // ---- For the demo: where things are in the unzoomed picture. ----
    internal Rect? FocusPanel() => IsOpen ? SpectatorDemoZoom.Measure(_zoom, _panel) : null;
    internal Rect? FocusDeathMarket() => IsOpen ? SpectatorDemoZoom.Measure(_zoom, _deathCard) : null;
    internal Rect? FocusDeathOption(int option) => IsOpen && option < _deathOptions.Count ? SpectatorDemoZoom.Measure(_zoom, _deathOptions[option]) : null;
    internal Rect? FocusBoard() => IsOpen ? SpectatorDemoZoom.Measure(_zoom, _board) : null;
    internal Rect? FocusResult() => SpectatorDemoZoom.Measure(_zoom, _result);
    internal Rect? FocusStake(int stake)
    {
        foreach (var button in _buttons) if (button.Kind == Hit.Stake && button.Value == stake) return IsOpen ? SpectatorDemoZoom.Measure(_zoom, button.Rect) : null;
        return null;
    }

    /// <summary>Queues a result card for a decided market; a footer replaces the points line.</summary>
    internal void ShowResult(string title, string answer, int delta, bool bet, string? footer = null) => _results.Enqueue((title, answer, delta, bet, false, footer));

    internal void Tick(float deltaTime, BettingState state, Func<ulong, string> name, ulong local, bool chinese, Vector2? pointer)
    {
        bool cursor = pointer.HasValue;
        _ui = SpectatorTextStyle.UiScale(Area);
        SpectatorDemoZoom.Apply(_zoom);
        _open = Mathf.MoveTowards(_open, IsOpen ? 1 : 0, deltaTime / .18f);
        bool active = _open > 0 || _resultAge < _resultSeconds || _results.Count > 0;
        if (_canvas.gameObject.activeSelf != active) _canvas.gameObject.SetActive(active);
        _panel.gameObject.SetActive(_open > 0);
        if (_open > 0)
        {
            float eased = 1 - (1 - _open) * (1 - _open);
            _panelGroup.alpha = eased; _panel.localScale = Vector3.one * (.96f + .04f * eased);
            // Redrawn on change, and when the pointer appears or goes (the "press P" hint).
            if (state.Revision != _renderedRevision || cursor != _renderedCursor || _confirmUntil > 0 && !ConfirmingOptOut) { if (!ConfirmingOptOut) _confirmUntil = 0; Render(state, name, local, chinese, cursor); }
            _scroll.Tick(deltaTime);
            Hover(pointer);
            TickToast(deltaTime);
        }
        TickResult(deltaTime, chinese);
    }

    private void Render(BettingState state, Func<ulong, string> name, ulong local, bool chinese, bool cursor)
    {
        _renderedRevision = state.Revision; _renderedCursor = cursor; _usedImages = _usedTexts = 0; _buttons.Clear(); _buttonColors.Clear(); _hovered = -1; _layer = _panel;
        _deathCard = _board = null; _deathOptions.Clear();
        float u = _ui, pad = Mathf.Round(12 * u), width = Mathf.Round(640 * u), boardWidth = Mathf.Round(170 * u), font = Mathf.Round(13 * u);
        float left = pad, cardsWidth = width - boardWidth - pad * 3, y = pad;
        var title = Text(chinese ? "观众竞猜" : "Audience bets", left, y, cardsWidth, Mathf.Round(22 * u), Mathf.Round(17 * u), Accent);
        // Without a pointer the panel says how to get one, beside its title where nothing covers it.
        if (!cursor)
        {
            float titleWidth = Mathf.Ceil(title.GetPreferredValues(title.text, 4096, 0).x) + Mathf.Round(12 * u);
            Text(chinese ? "按 P 显示光标后点击下注" : "Press P for the pointer, then click", left + titleWidth, y + Mathf.Round(2 * u), cardsWidth - titleWidth - Mathf.Round(100 * u),
                Mathf.Round(22 * u), Mathf.Round(11 * u), Muted);
        }
        Text((chinese ? "我的积分 " : "My points ") + state.PointsOf(local), left, y, cardsWidth, Mathf.Round(22 * u), font, TextColor, TextAlignmentOptions.MidlineRight);
        y += Mathf.Round(28 * u);
        // Stake chips.
        Text(chinese ? "下注" : "Stake", left, y, Mathf.Round(48 * u), Mathf.Round(24 * u), font, Muted);
        float x = left + Mathf.Round(50 * u);
        foreach (int stake in BettingState.Stakes)
        {
            var chip = Fill(x, y, Mathf.Round(48 * u), Mathf.Round(24 * u), stake == _stake ? Selected : OptionColor);
            Text(stake.ToString(), x, y, Mathf.Round(48 * u), Mathf.Round(24 * u), font, TextColor, TextAlignmentOptions.Center);
            _buttons.Add((chip.rectTransform, Hit.Stake, 0, stake));
            x += Mathf.Round(54 * u);
        }
        // Opting out takes a second click, so a stray click never hides the game for the session.
        float optWidth = Mathf.Round(150 * u);
        var opt = Fill(left + cardsWidth - optWidth, y, optWidth, Mathf.Round(24 * u), ConfirmingOptOut ? new Color(.45f, .1f, .07f, 1) : OptionColor);
        Text(ConfirmingOptOut ? (chinese ? "再次点击确认不参与" : "Click again to confirm") : (chinese ? "本局不参与竞猜" : "Skip bets this game"),
            left + cardsWidth - optWidth, y, optWidth, Mathf.Round(24 * u), Mathf.Round(12 * u), ConfirmingOptOut ? Bad : Muted, TextAlignmentOptions.Center);
        _buttons.Add((opt.rectTransform, Hit.OptOut, 0, 0));
        y += Mathf.Round(34 * u);
        float header = y;
        _layer = _scroll.Content; y = 0; left = 0;
        // Markets: open first, then the latest results.
        var ordered = new List<BetMarket>(state.Markets);
        ordered.Sort((a, b) => a.Status == b.Status ? b.Id.CompareTo(a.Id) : a.Status == BetMarketStatus.Open ? -1 : b.Status == BetMarketStatus.Open ? 1 : b.Id.CompareTo(a.Id));
        if (ordered.Count == 0)
        { Text(chinese ? "落地后、仍有两名以上存活玩家时开盘" : "Bets open after landing while two or more players are alive", left, y, cardsWidth, Mathf.Round(24 * u), font, Muted); y += Mathf.Round(30 * u); }
        int shown = 0;
        foreach (var market in ordered)
        {
            if (shown++ == 4) break;
            y = Card(market, state, name, local, chinese, left, y, cardsWidth) + Mathf.Round(8 * u);
        }
        // Leaderboard: everyone in the game (up to 32), beside the markets.
        float bx = width - boardWidth - pad * 2, by = 0;
        var board = new List<KeyValuePair<ulong, int>>(state.Points);
        board.Sort((a, b) => b.Value.CompareTo(a.Value));
        _board = Fill(bx, by, boardWidth, Mathf.Round(24 * u) * (board.Count + 1) + Mathf.Round(4 * u), CardColor).rectTransform;
        Text(chinese ? "积分榜" : "Leaderboard", bx + pad * .5f, by, boardWidth, Mathf.Round(24 * u), font, Accent);
        for (int i = 0; i < board.Count; i++)
        {
            float row = by + Mathf.Round(24 * u) * (i + 1);
            var color = board[i].Key == local ? Accent : TextColor;
            Text($"{i + 1}. {name(board[i].Key)}", bx + pad * .5f, row, boardWidth - Mathf.Round(46 * u), Mathf.Round(22 * u), font, color);
            Text(board[i].Value.ToString(), bx, row, boardWidth - pad * .5f, Mathf.Round(22 * u), font, color, TextAlignmentOptions.MidlineRight);
        }
        // The panel grows with its content up to most of the screen below any top inset; the rest scrolls.
        float content = Mathf.Max(y, Mathf.Round(24 * u) * (board.Count + 1) + Mathf.Round(4 * u));
        float view = Mathf.Min(content, Mathf.Max(Mathf.Round(120 * u), (Area.y - TopInset) * .82f - header - pad));
        _scroll.Layout(pad, header, width - pad * 2, view, content, u);
        _panel.sizeDelta = new Vector2(width, header + view + pad);
        _panel.anchoredPosition = new Vector2(0, -TopInset * .5f);
        foreach (var button in _buttons) _buttonColors.Add(button.Rect.GetComponent<Image>().color);
        _toast.anchoredPosition = new Vector2(width * .5f, -Mathf.Round(36 * u)); _toast.SetAsLastSibling();
        for (int i = _usedImages; i < _images.Count; i++) _images[i].gameObject.SetActive(false);
        for (int i = _usedTexts; i < _texts.Count; i++) _texts[i].gameObject.SetActive(false);
    }

    private float Card(BetMarket market, BettingState state, Func<ulong, string> name, ulong local, bool chinese, float left, float top, float width)
    {
        float u = _ui, pad = Mathf.Round(8 * u), font = Mathf.Round(13 * u), line = Mathf.Round(22 * u), optionHeight = Mathf.Round(30 * u);
        var mine = market.BetOf(local);
        int pool = market.Pool;
        float y = top + pad;
        var card = Fill(left, top, width, 1, CardColor);
        bool death = _deathCard == null && market.Kind == BetMarketKind.NextDeath && market.Status == BetMarketStatus.Open;
        if (death) _deathCard = card.rectTransform;
        string title = Title(market.Kind, chinese);
        string status = market.Status == BetMarketStatus.Open ? (chinese ? $"进行中 · 奖池 {pool}" : $"Open · pool {pool}")
            : market.Status == BetMarketStatus.Settled ? (chinese ? "已结算" : "Settled") : (chinese ? "已退还" : "Refunded");
        Text(title, left + pad, y, width - pad * 2, line, Mathf.Round(14 * u), TextColor);
        Text(status, left + pad, y, width - pad * 2, line, Mathf.Round(12 * u), market.Status == BetMarketStatus.Open ? Accent : Muted, TextAlignmentOptions.MidlineRight);
        y += line + Mathf.Round(4 * u);
        float x = left + pad, columns = 3, optionWidth = Mathf.Floor((width - pad * 2 - Mathf.Round(6 * u) * (columns - 1)) / columns);
        for (int i = 0; i < market.Options.Count; i++)
        {
            if (i > 0 && i % 3 == 0) { x = left + pad; y += optionHeight + Mathf.Round(6 * u); }
            bool winner = market.Winners.Contains(i), picked = mine != null && mine.Option == i;
            var color = winner ? Winner : picked ? Selected : market.Status == BetMarketStatus.Open ? OptionColor : Dim;
            var option = Fill(x, y, optionWidth, optionHeight, color);
            if (death) _deathOptions.Add(option.rectTransform);
            int staked = market.Staked(i);
            string label = OptionLabel(market, i, name, chinese);
            Text(label, x + Mathf.Round(6 * u), y, optionWidth - Mathf.Round(52 * u), optionHeight, font, TextColor);
            Text(pool > 0 ? $"{staked * 100 / pool}%" : "—", x, y, optionWidth - Mathf.Round(6 * u), optionHeight, Mathf.Round(12 * u), Muted, TextAlignmentOptions.MidlineRight);
            if (market.Status == BetMarketStatus.Open && mine == null && !(market.Kind == BetMarketKind.NextDeath && market.Options[i] == local))
                _buttons.Add((option.rectTransform, Hit.Option, market.Id, i));
            x += optionWidth + Mathf.Round(6 * u);
        }
        y += optionHeight + Mathf.Round(4 * u);
        string footer = mine == null ? (market.Status == BetMarketStatus.Open ? (chinese ? $"点击选项押 {_stake} 分" : $"Click an option to stake {_stake}") : (chinese ? "未参与" : "No bet"))
            : market.Status == BetMarketStatus.Open ? (chinese ? $"你押了 {mine.Stake} 分" : $"You staked {mine.Stake}")
            : (chinese ? $"你押 {mine.Stake} · 返还 {mine.Payout}" : $"Staked {mine.Stake} · returned {mine.Payout}");
        Text(footer, left + pad, y, width - pad * 2, Mathf.Round(18 * u), Mathf.Round(12 * u), Muted);
        y += Mathf.Round(18 * u) + pad;
        PlaceRect(card.rectTransform, left, top, width, y - top);
        return y;
    }

    // The button under the pointer lightens; it is the one a click would press.
    private void Hover(Vector2? pointer)
    {
        int hovered = -1;
        if (pointer is { } point)
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (Reachable(_buttons[i].Rect, point) && RectTransformUtility.RectangleContainsScreenPoint(_buttons[i].Rect, point, Eye))
                { hovered = i; break; }
        }
        if (hovered == _hovered) return;
        if (_hovered >= 0 && _hovered < _buttons.Count) _buttons[_hovered].Rect.GetComponent<Image>().color = _buttonColors[_hovered];
        _hovered = hovered;
        if (hovered >= 0) _buttons[hovered].Rect.GetComponent<Image>().color = _buttonColors[hovered] + new Color(.09f, .07f, .05f, 0);
    }

    private void TickToast(float deltaTime)
    {
        if (!_toast.gameObject.activeSelf) return;
        _toastAge += deltaTime;
        _toastGroup.alpha = Mathf.Clamp01(_toastAge / .12f) * Mathf.Clamp01((2.2f - _toastAge) / .4f);
        _toast.localScale = Vector3.one * (1 + .08f * Mathf.Max(0, 1 - _toastAge / .2f));
        if (_toastAge > 2.2f) _toast.gameObject.SetActive(false);
    }

    internal static string Title(BetMarketKind kind, bool chinese) => kind == BetMarketKind.NextDeath
        ? (chinese ? "谁下一个阵亡？" : "Who dies next?") : (chinese ? "本局有人活着离开吗？" : "Does anyone get out alive?");
    internal static string OptionLabel(BetMarket market, int option, Func<ulong, string> name, bool chinese) => market.Kind == BetMarketKind.NextDeath
        ? name(market.Options[option]) : market.Options[option] == 1 ? (chinese ? "有人生还" : "Yes") : (chinese ? "全员阵亡" : "No");

    /// <summary>
    /// The result line: the answer (two names for players who died together, the first two and a count for more),
    /// noting a refund when nobody picked it; an undecided market says so.
    /// </summary>
    internal static string Answer(BetMarket market, Func<ulong, string> name, bool chinese)
    {
        if (market.Winners.Count == 0) return chinese ? "未分胜负，全部退还" : "Undecided: stakes returned";
        string answer = OptionLabel(market, market.Winners[0], name, chinese);
        if (market.Winners.Count == 2) answer += (chinese ? "、" : ", ") + OptionLabel(market, market.Winners[1], name, chinese) + (chinese ? "（同时阵亡）" : " (together)");
        else if (market.Winners.Count > 2) answer += chinese ? $" 等 {market.Winners.Count} 人同时阵亡" : $" and {market.Winners.Count - 1} more, together";
        return (chinese ? "答案：" : "Answer: ") + answer
            + (market.Status == BetMarketStatus.Refunded ? chinese ? " · 无人押中，全部退还" : " · nobody won, stakes returned" : string.Empty);
    }

    // The result card flips in horizontally, counts the points up, then fades away.
    private void TickResult(float deltaTime, bool chinese)
    {
        if (_resultAge >= _resultSeconds)
        {
            if (_results.Count == 0) { if (_result.gameObject.activeSelf) _result.gameObject.SetActive(false); return; }
            _current = _results.Dequeue(); _resultAge = 0; _deltaShown = int.MinValue; _resultSeconds = _results.Count > 0 ? 2.5f : 4.5f;
            float u = _ui, width = Mathf.Round(340 * u);
            _resultTitle.text = _current.Notice ? _current.Title : (chinese ? "结算 · " : "Result · ") + _current.Title;
            _resultAnswer.text = _current.Answer;
            _resultTitle.fontSize = Mathf.Round(13 * u); _resultAnswer.fontSize = Mathf.Round(17 * u); _resultDelta.fontSize = Mathf.Round(20 * u);
            _resultTitle.color = Muted; _resultAnswer.color = TextColor;
            PlaceRect(_resultTitle.rectTransform, 0, Mathf.Round(10 * u), width, Mathf.Round(18 * u));
            PlaceRect(_resultAnswer.rectTransform, 0, Mathf.Round(30 * u), width, Mathf.Round(24 * u));
            PlaceRect(_resultDelta.rectTransform, 0, Mathf.Round(56 * u), width, Mathf.Round(28 * u));
            _result.sizeDelta = new Vector2(width, Mathf.Round(92 * u));
            _result.anchoredPosition = new Vector2(0, -Mathf.Round(70 * u) - TopInset);
            _result.gameObject.SetActive(true);
        }
        _resultAge += deltaTime;
        float age = _resultAge;
        float flip = Mathf.Clamp01(age / .35f), eased = 1 - Mathf.Pow(1 - flip, 3);
        _result.localScale = new Vector3(Mathf.Max(.02f, eased), 1, 1);
        _resultGroup.alpha = Mathf.Clamp01((_resultSeconds - age) / .5f);
        // Count the points up over the first second after the flip.
        int delta = _current.Bet ? Mathf.RoundToInt(_current.Delta * Mathf.Clamp01((age - .35f) / 1f)) : 0;
        if (delta != _deltaShown)
        {
            _deltaShown = delta;
            _resultDelta.text = _current.Notice ? string.Empty : _current.Footer ?? (!_current.Bet ? (chinese ? "你未参与这一局" : "You did not bet")
                : (delta >= 0 ? "+" : "") + delta + (chinese ? " 分" : " pts"));
            _resultDelta.color = !_current.Bet || _current.Footer != null ? Muted : _current.Delta >= 0 ? Good : Bad;
        }
    }

    private Image Fill(float x, float y, float width, float height, Color color)
    {
        if (_usedImages == _images.Count) _images.Add(SplitScreenView.CreateImage("Betting fill", _layer, color));
        var image = _images[_usedImages++]; image.gameObject.SetActive(true); image.color = color;
        if (image.transform.parent != _layer) image.transform.SetParent(_layer, false);
        image.transform.SetAsLastSibling();
        PlaceRect(image.rectTransform, x, y, width, height);
        return image;
    }
    private TextMeshProUGUI Text(string value, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        if (_usedTexts == _texts.Count) _texts.Add(Label(_layer, "Betting text"));
        var text = _texts[_usedTexts++]; text.gameObject.SetActive(true);
        if (text.transform.parent != _layer) text.transform.SetParent(_layer, false);
        text.transform.SetAsLastSibling();
        text.text = value; text.fontSize = size; text.color = color; text.alignment = align;
        PlaceRect(text.rectTransform, x, y, width, height);
        return text;
    }
    private TextMeshProUGUI Label(Transform parent, string name) { var text = SplitScreenView.CreateText(name, parent); Apply(text); return text; }
    private void Apply(TextMeshProUGUI text) { text.font = _font; if (_material != null) text.fontSharedMaterial = _material; }
    private static void PlaceRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    public void Dispose()
    {
        if (_material != null) UnityEngine.Object.Destroy(_material);
        UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The early-leave vote strip, above the views (they make room for it as it arrives): a cell per vote needed, filled
/// for each vote cast, with the count, and "voted" on its left once this player has voted. Holding the button turns
/// the cells into its progress bar. A crowd too large for cells shows one bar. Once the ship's leaving is set the
/// strip shows when it leaves. A vote that passes while the strip shows it ends in stages: this player's own vote as
/// cast ("voted", for 2 s even when it was the last or only one), then the vote as passed ("leaving", 2 s), then the
/// strip fades out as it was and comes back as the departure time (the views keep their room meanwhile). A first
/// vote only brings the strip in once the button has been held a moment.
/// </summary>
internal sealed partial class SplitScreenView
{
    private const float VoteFadeSeconds = .25f, VoteShowDelay = .15f, VoteHideDelay = .4f, VotePopSeconds = .35f,
        VoteTagSeconds = .3f, VoteMixSeconds = .2f, VotedHoldSeconds = 2f, PassedHoldSeconds = 2f;
    private const int VoteCellLimit = 12;
    private static readonly Color VoteOrange = new Color(1, .45f, .12f, 1), VoteDim = new Color(1, .45f, .12f, .22f),
        VoteEdge = new Color(1, .38f, .02f, .55f), VoteInk = new Color(.12f, .06f, .02f, 1), VoteDeparture = new Color(1, .42f, .2f, 1);
    private RectTransform _strip = null!, _stripTag = null!, _stripCells = null!, _stripBar = null!, _stripBarFill = null!;
    private CanvasGroup _stripGroup = null!, _stripCellsGroup = null!, _stripBarGroup = null!;
    private TextMeshProUGUI _stripTagText = null!, _stripCount = null!, _stripDeparture = null!;
    private Image[] _stripEdges = null!;
    private readonly List<Image> _stripCellImages = new List<Image>();
    private SplitScreenLeaveVoteState _vote, _voteShown;
    private bool _voteChinese = true, _voteDirty = true, _stripVisible;
    private float _voteWantFor, _voteLostFor, _voteTag, _voteMix, _voteBar, _votePop = VotePopSeconds;
    // The strip shows the departure time (else the vote): it only changes while the strip is faded out.
    private bool _voteDeparting;
    // A passed vote's ending: seconds into it (-1: none), how long its "voted" stage lasts, and whether it shows as passed.
    private float _voteEnd = -1, _voteVotedStage, _votedFor;
    private bool _votePassed;
    private int _votePopIndex = -1;
    private float _tagWidth, _countWidth, _departureWidth;

    /// <summary>Room the views leave above themselves for the strip.</summary>
    private float LeaveVoteReserve => _stripVisible ? Mathf.Round(32 * _uiScale) : 0;

    private void CreateLeaveVote()
    {
        _strip = CreateImage("Leave vote strip", _hudRoot, new Color(.02f, .022f, .026f, .82f)).rectTransform;
        _strip.anchorMin = _strip.anchorMax = _strip.pivot = new Vector2(.5f, 1);
        _stripEdges = Frame4("Leave vote strip edge ", _strip, VoteEdge, 1);
        _stripGroup = _strip.gameObject.AddComponent<CanvasGroup>(); _stripGroup.alpha = 0;
        _stripTag = CreateImage("Leave vote voted", _strip, VoteOrange).rectTransform;
        _stripTag.gameObject.AddComponent<RectMask2D>();
        _stripTagText = CreateText("Leave vote voted text", _stripTag); _stripTagText.color = VoteInk;
        _stripTagText.alignment = TextAlignmentOptions.MidlineLeft; _stripTagText.enableWordWrapping = false;
        _stripCells = new GameObject("Leave vote cells", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        _stripCells.SetParent(_strip, false); _stripCellsGroup = _stripCells.GetComponent<CanvasGroup>();
        _stripBar = CreateImage("Leave vote bar", _strip, VoteDim).rectTransform;
        _stripBarGroup = _stripBar.gameObject.AddComponent<CanvasGroup>();
        _stripBarFill = CreateImage("Leave vote bar fill", _stripBar, VoteOrange).rectTransform;
        _stripBarFill.anchorMin = Vector2.zero; _stripBarFill.anchorMax = new Vector2(0, 1); _stripBarFill.pivot = new Vector2(0, .5f);
        _stripCount = CreateText("Leave vote count", _strip); _stripCount.color = VoteOrange; _stripCount.alignment = TextAlignmentOptions.MidlineLeft;
        _stripDeparture = CreateText("Leave vote departure", _strip); _stripDeparture.color = VoteDeparture; _stripDeparture.alignment = TextAlignmentOptions.Center;
        // The HUD clock may include formatting, for example LCBetterClock's dark leading zero.
        _stripDeparture.richText = true;
        foreach (var rect in new[] { _stripTag, _stripCells, _stripBar, (RectTransform)_stripTagText.transform, (RectTransform)_stripCount.transform, (RectTransform)_stripDeparture.transform })
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
        _strip.gameObject.SetActive(false);
    }
    private void ConfigureLeaveVoteFont() { ApplyFont(_stripTagText); ApplyFont(_stripCount); ApplyFont(_stripDeparture); _voteDirty = true; }
    private void ResetLeaveVoteLayout() => _voteDirty = true;

    /// <summary>The game's early-leave vote as this dead player has it; a default state hides the strip.</summary>
    internal void SetLeaveVote(SplitScreenLeaveVoteState vote, bool chinese)
    {
        if (vote.Votes > _vote.Votes && vote.Departure == null) { _votePopIndex = vote.Votes - 1; _votePop = 0; }
        if (vote.Votes != _vote.Votes || vote.Needed != _vote.Needed || vote.Departure != _vote.Departure || chinese != _voteChinese) _voteDirty = true;
        _vote = vote; _voteChinese = chinese;
    }

    private void TickLeaveVote(float deltaTime)
    {
        var vote = _vote;
        bool wanted = vote.Departure != null || vote.Prompt && (vote.Votes > 0 || vote.Hold > 0);
        _voteWantFor = wanted ? _voteWantFor + deltaTime : 0;
        _voteLostFor = wanted ? 0 : _voteLostFor + deltaTime;
        // A first vote waits for a deliberate hold; a lost one lingers briefly so a released button does not flicker.
        // The wait only keeps a brief press from bringing in a hidden strip: holding again while it lingers or fades
        // out picks it up where it is (no dip out and back in, the views not moving away and back).
        bool firstVote = vote.Departure == null && vote.Votes == 0;
        bool visible = wanted ? !firstVote || _stripGroup.alpha > 0 || _voteWantFor >= VoteShowDelay : _stripVisible && _voteLostFor < VoteHideDelay;
        if (visible != _stripVisible) { _stripVisible = visible; _layoutDirty = true; }
        // How long this player's vote has been on show as cast.
        _votedFor = _voteShown.Voted && _voteEnd < 0 && _stripGroup.alpha > 0 ? _votedFor + deltaTime : _voteEnd < 0 ? 0 : _votedFor;
        if (vote.Departure != null && !_voteDeparting && _voteEnd < 0 && _stripGroup.alpha > 0) BeginVoteEnd(vote);
        else if (vote.Departure == null && _voteEnd >= 0) { _voteEnd = -1; _votePassed = false; _voteDirty = true; }
        if (_voteEnd >= 0)
        {
            _voteEnd += deltaTime;
            if (!_votePassed && _voteEnd >= _voteVotedStage) { _votePassed = true; _voteDirty = true; _votePop = 0; _votePopIndex = -1; }
        }
        bool ending = _voteEnd >= 0 && _voteEnd < _voteVotedStage + PassedHoldSeconds;
        // Turning from the vote to the departure time, the vote fades out as it last was before the time fades in.
        bool turning = wanted && (vote.Departure != null) != _voteDeparting && _stripGroup.alpha > 0 && !ending;
        if (wanted && !turning && _voteEnd < 0)
        {
            _voteShown = vote;
            // Starting from hidden has no old vote to finish; enter the new content before the first fade-in frame.
            _voteDeparting = vote.Departure != null;
        }
        float alpha = Mathf.MoveTowards(_stripGroup.alpha, visible && !turning ? 1 : 0, deltaTime / VoteFadeSeconds);
        _stripGroup.alpha = alpha;
        if (_strip.gameObject.activeSelf != alpha > 0) _strip.gameObject.SetActive(alpha > 0);
        if (alpha <= 0)
        {
            // Hidden, it starts afresh: the next hold grows its bar from empty, not from where the last one was.
            _voteBar = 0; _voteMix = vote.Hold > 0 && !vote.Voted || vote.Needed > VoteCellLimit ? 1 : 0;
            if (_voteDeparting != (vote.Departure != null)) { _voteDeparting = vote.Departure != null; _voteShown = vote; _voteDirty = true; }
            _voteEnd = -1; _votePassed = false;
            return;
        }
        LayoutLeaveVote(_voteShown, deltaTime);
        _strip.anchoredPosition = new Vector2(0, -StripTop() + (1 - SplitScreenResultsPanel.EaseOut(alpha)) * 8 * _uiScale);
    }

    // The vote passed while on show: every cell filled (the last vote landing with its pop), this player's vote kept
    // as cast for at least a moment, then shown as passed.
    private void BeginVoteEnd(SplitScreenLeaveVoteState departure)
    {
        int needed = Mathf.Max(_voteShown.Needed, departure.Votes);
        if (departure.Votes > _voteShown.Votes) { _votePopIndex = Mathf.Min(departure.Votes, needed) - 1; _votePop = 0; }
        bool voted = departure.Voted || _voteShown.Voted;
        _voteVotedStage = voted ? Mathf.Max(0, VotedHoldSeconds - _votedFor) : 0;
        _voteShown = new SplitScreenLeaveVoteState(true, voted, _voteShown.Controller, needed, needed, 0, null);
        _voteEnd = 0; _votePassed = false; _voteDirty = true;
    }

    // Midway between the screen's top and the view right below the strip (else the highest view), as the views move.
    private float StripTop()
    {
        float height = _strip.sizeDelta.y, half = _strip.sizeDelta.x / 2, centre = _hudRoot.rect.center.x, rootTop = _hudRoot.rect.yMax, below = float.MaxValue, highest = float.MaxValue;
        foreach (var tile in _tiles.Values)
        {
            if (tile.Retiring) continue;
            tile.Decoration.GetWorldCorners(_clockCorners);
            Vector3 left = _hudRoot.InverseTransformPoint(_clockCorners[1]), right = _hudRoot.InverseTransformPoint(_clockCorners[2]);
            float gap = rootTop - left.y;
            highest = Mathf.Min(highest, gap);
            if (left.x < centre + half && right.x > centre - half) below = Mathf.Min(below, gap);
        }
        float room = below < float.MaxValue ? below : highest;
        return room < float.MaxValue ? Mathf.Max(Mathf.Round(6 * _uiScale), (room - height) / 2) : Mathf.Round(6 * _uiScale);
    }

    private void LayoutLeaveVote(SplitScreenLeaveVoteState vote, float deltaTime)
    {
        float s = _uiScale, height = Mathf.Round(26 * s), pad = Mathf.Round(8 * s), gap = Mathf.Round(6 * s);
        float cellWidth = Mathf.Round(12 * s), cellHeight = Mathf.Round(10 * s), cellGap = Mathf.Round(3 * s), tagHeight = Mathf.Round(18 * s);
        bool departure = _voteDeparting, many = vote.Needed > VoteCellLimit;
        int cells = many ? 0 : vote.Needed;
        if (_voteDirty)
        {
            _voteDirty = false;
            float font = Mathf.Round(14 * s);
            _stripTagText.text = _votePassed ? _voteChinese ? "确认起飞" : "Leaving" : _voteChinese ? "你已投票" : "Voted";
            _stripCount.text = $"{vote.Votes}/{vote.Needed}";
            if (departure) _stripDeparture.text = (_voteChinese ? "起飞 " : "Leaves ") + vote.Departure;
            _stripTagText.fontSize = _stripCount.fontSize = _stripDeparture.fontSize = font;
            _tagWidth = Mathf.Ceil(_stripTagText.GetPreferredValues(_stripTagText.text, 4096, 0).x) + pad * 1.5f;
            _countWidth = Mathf.Ceil(_stripCount.GetPreferredValues(_stripCount.text, 4096, 0).x);
            _departureWidth = Mathf.Ceil(_stripDeparture.GetPreferredValues(_stripDeparture.text, 4096, 0).x);
            while (_stripCellImages.Count < Mathf.Min(cells, VoteCellLimit))
            {
                var cell = CreateImage("Leave vote cell " + _stripCellImages.Count, _stripCells, VoteDim);
                cell.rectTransform.anchorMin = cell.rectTransform.anchorMax = cell.rectTransform.pivot = new Vector2(0, .5f);
                _stripCellImages.Add(cell);
            }
            var tagText = _stripTagText.rectTransform; tagText.sizeDelta = new Vector2(_tagWidth, tagHeight); tagText.anchoredPosition = new Vector2(pad * .75f, 0);
        }
        bool holding = vote.Hold > 0 && !vote.Voted;
        _voteTag = Mathf.MoveTowards(_voteTag, (vote.Voted || _votePassed) && !departure ? 1 : 0, deltaTime / VoteTagSeconds);
        _voteMix = Mathf.MoveTowards(_voteMix, holding || many ? 1 : 0, deltaTime / VoteMixSeconds);
        _voteBar = Mathf.Lerp(_voteBar, holding ? vote.Hold : vote.Needed > 0 ? (float)vote.Votes / vote.Needed : 0, 1 - Mathf.Exp(-18 * deltaTime));
        _votePop = Mathf.Min(VotePopSeconds, _votePop + deltaTime);
        float pop = 1 - SplitScreenResultsPanel.EaseOut(_votePop / VotePopSeconds);

        _stripDeparture.gameObject.SetActive(departure);
        _stripTag.gameObject.SetActive(!departure && _voteTag > 0);
        _stripCells.gameObject.SetActive(!departure); _stripBar.gameObject.SetActive(!departure); _stripCount.gameObject.SetActive(!departure);
        float width;
        if (departure)
        {
            var text = _stripDeparture.rectTransform; text.sizeDelta = new Vector2(_departureWidth + 2, height); text.anchoredPosition = new Vector2(pad, 0);
            width = pad * 2 + _departureWidth;
        }
        else
        {
            float cellsWidth = cells > 0 ? cells * cellWidth + (cells - 1) * cellGap : 0;
            float area = many ? Mathf.Round(120 * s) : Mathf.Max(cellsWidth, Mathf.Round(72 * s));
            float tag = _tagWidth * SplitScreenResultsPanel.EaseOut(_voteTag), x = pad;
            _stripTag.sizeDelta = new Vector2(tag, tagHeight); _stripTag.anchoredPosition = new Vector2(x, 0);
            if (tag > 0) x += tag + gap;
            _stripCells.sizeDelta = new Vector2(cellsWidth, cellHeight); _stripCells.anchoredPosition = new Vector2(x + (area - cellsWidth) / 2, 0);
            for (int i = 0; i < _stripCellImages.Count; i++)
            {
                var cell = _stripCellImages[i];
                if (cell.gameObject.activeSelf != i < cells) cell.gameObject.SetActive(i < cells);
                if (i >= cells) continue;
                cell.rectTransform.sizeDelta = new Vector2(cellWidth, cellHeight);
                cell.rectTransform.anchoredPosition = new Vector2(i * (cellWidth + cellGap), 0);
                cell.color = i < vote.Votes ? VoteOrange : VoteDim;
                // The newest vote's cell lands with a pop.
                cell.rectTransform.localScale = Vector3.one * (i == _votePopIndex ? 1 + .7f * pop : 1);
            }
            _stripCellsGroup.alpha = 1 - _voteMix; _stripBarGroup.alpha = _voteMix;
            _stripBar.sizeDelta = new Vector2(area, Mathf.Round(6 * s)); _stripBar.anchoredPosition = new Vector2(x, 0);
            _stripBarFill.sizeDelta = new Vector2(area * Mathf.Clamp01(_voteBar), 0);
            x += area + gap;
            var count = _stripCount.rectTransform; count.sizeDelta = new Vector2(_countWidth + 2, height); count.anchoredPosition = new Vector2(x, 0);
            count.localScale = Vector3.one * (1 + .25f * pop);
            width = x + _countWidth + pad;
        }
        _strip.sizeDelta = new Vector2(width, height);
        var edge = Color.Lerp(VoteEdge, VoteOrange, Mathf.Max(holding ? vote.Hold : 0, pop));
        foreach (var image in _stripEdges) image.color = edge;
    }

    private void ClearLeaveVote()
    {
        _vote = _voteShown = default; _stripVisible = false; _voteWantFor = _voteLostFor = _voteTag = _voteMix = _voteBar = _votedFor = 0; _voteDeparting = _votePassed = false; _voteEnd = -1;
        _votePop = VotePopSeconds; _votePopIndex = -1; _stripGroup.alpha = 0; _strip.gameObject.SetActive(false); _voteDirty = true;
    }
}

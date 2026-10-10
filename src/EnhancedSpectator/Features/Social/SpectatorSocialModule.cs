using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SplitScreen;
using EnhancedSpectator.GameInterop;
using EnhancedSpectator.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// Dead players' social features: emotes (to the ghost heads and the split-screen audience row), audience bets, and
/// teammate ratings whose summary every mod user sees when the round ends. During the split-screen preview the same
/// panels run on a local <see cref="SocialPreview"/> instead of the network, driven by the test toolbar.
/// </summary>
internal sealed class SpectatorSocialModule : IFeatureModule, IRuntimeTickable, IRuntimeLateTickable
{
    internal static SpectatorSocialModule? Current { get; private set; }
    private static readonly KeyCode[] Numbers = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
    private readonly EnhancedSpectatorConfig _config;
    private readonly LethalCompanySpectatorAdapter _input = new LethalCompanySpectatorAdapter();
    private readonly List<(int Slot, string Name, bool Alive)> _previewPeople = new List<(int, string, bool)>(32);
    private readonly List<BetMarket> _previewResolved = new List<BetMarket>();
    private SpectatorEmotePicker? _picker;
    private SpectatorBettingPanel? _panel;
    private SpectatorRatingPanel? _ratingPanel;
    private SocialPreview? _preview;
    private float _previewCooldownUntil;
    // The game (lobby session) in which the local player declined the bets; they stay hidden until it ends.
    private readonly BetParticipation _participation = new BetParticipation();
    private int Session => Network.Game.Session;
    private bool OptedOut => _participation.Skipped(_preview != null, Session);

    internal SocialNetworkService Network { get; }
    internal BettingService Betting { get; }
    internal RatingService Rating { get; }
    internal SpectatorChatGroup Chat { get; }

    // The book, identities and names the panels show: the preview's while it runs, otherwise the host's.
    private BettingState Book => _preview?.Book ?? Betting.State;
    private ulong LocalId => _preview != null ? SocialPreview.LocalId : Network.LocalClientId;
    private string NameOf(ulong id) => _preview != null ? _preview.NameOf(id) : Betting.NameOf(id);
    // Bets run when the host turns them on; the preview stands in for a host (the local option, or the demo showing them).
    private bool BetsOn => _preview != null ? _config.Camera.EnableAudienceBets.Value || SplitScreenModule.Current?.DemoRunning == true : Network.BetsEnabled;
    // Reviews need a host running this mod.
    private bool ReviewsOn => _preview != null || Network.Ready;
    // Emotes also work without the mod on the host, through the Steam lobby (not on LAN).
    private bool EmotesOn => _preview != null || Network.CanEmote;
    private float EmoteCooldown => _preview != null ? Mathf.Max(0, _previewCooldownUntil - Time.realtimeSinceStartup) : Network.CooldownRemaining;

    internal SpectatorSocialModule(EnhancedSpectatorConfig config)
    {
        _config = config;
        Network = new SocialNetworkService(config);
        Network.EmoteReceived += OnEmote;
        Betting = new BettingService(Network);
        Betting.Resolved += ShowResult;
        Rating = new RatingService(Network);
        Rating.Summary += OnRatingSummary;
        Chat = new SpectatorChatGroup(config, Network);
    }

    public void Initialize() => Current = this;

    /// <summary>Whether the open picker is under the pointer; its clicks are not for views beneath it.</summary>
    internal bool PickerContains(Vector2 point) => _picker != null && _picker.HitTest(point) >= 0 || _panel != null && _panel.Contains(point)
        || _ratingPanel != null && _ratingPanel.Contains(point);

    public void Tick()
    {
        Network.Tick();
        Betting.Tick();
        Rating.Tick();
        Chat.Tick();
        TickPreview();
        SpectatorSocialAvailability.Bets = BetsOn; SpectatorSocialAvailability.Reviews = ReviewsOn; SpectatorSocialAvailability.Emotes = EmotesOn;
        if (!EmotesOn) _picker?.Close();
        if (!BetsOn) _panel?.Close();
        if (!ReviewsOn) _ratingPanel?.Close();
        var mouse = Mouse.current;
        // The wheel scrolls a long panel under the pointer, or the round summary of a big crew (seen alive, in orbit).
        float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0;
        if (wheel != 0)
        {
            float notches = Mathf.Abs(wheel) >= 10 ? wheel / 120f : wheel;
            Vector2 point = mouse!.position.ReadValue();
            if (Cursor.visible) _panel?.Scroll(point, notches);
            _ratingPanel?.Scroll(point, notches);
        }
        // The preview is used with the ESC menu open, so only typing holds its keys back.
        bool blocked = _preview != null ? _input.IsTypingBlocked() : _input.IsUiInputBlocked();
        bool rateKey = !blocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.RateKey.Value);
        // The summary shows after everyone is revived, so its key works for the living too.
        if (rateKey && _ratingPanel?.SummaryShowing == true) { _ratingPanel.DismissSummary(); rateKey = false; }
        bool dead = Network.Game.LocalDead;
        // The preview keeps its panels through the ESC menu, where its toolbar opens them.
        if (_preview == null && (!dead || blocked || !RuntimeConnectionState.CanRunLocalDiagnostics(out _)))
        { _picker?.Close(); _panel?.Close(); _ratingPanel?.Close(); return; }
        if (rateKey && ReviewsOn) RatingPanel().Toggle();
        if (!blocked && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.BetKey.Value)) OpenBets();
        if (!blocked && EmotesOn && SpectatorInputService.IsKeyPressedThisFrame(_config.Camera.EmoteKey.Value)) TogglePicker();
        if (_picker?.IsOpen == true && !blocked)
            for (int i = 0; i < Numbers.Length; i++)
                if (SpectatorInputService.IsKeyPressedThisFrame(Numbers[i])) { Send(i); break; }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.visible) Click(mouse.position.ReadValue());
    }

    public void LateTick()
    {
        // The panels light up what is under a visible pointer (the demo's simulated one while it plays).
        Vector2? pointer = SplitScreenModule.Current?.DemoPointer ?? (Cursor.visible && Mouse.current != null ? Mouse.current.position.ReadValue() : null);
        bool chinese = _config.UseChineseText;
        _picker?.Tick(Time.unscaledDeltaTime, EmoteCooldown, chinese, pointer);
        if (_panel != null) { _panel.TopInset = SplitScreenModule.Current?.TopReserved ?? 0; _panel.Tick(Time.unscaledDeltaTime, Book, NameOf, LocalId, chinese, pointer); }
        if (_ratingPanel != null) _ratingPanel.TopInset = SplitScreenModule.Current?.TopReserved ?? 0;
        if (_preview != null) _ratingPanel?.Tick(Time.unscaledDeltaTime, _preview.Mine, SocialPreview.LocalId, _preview.RoundOpen, _preview.CopyTargetsTo, chinese, pointer);
        else _ratingPanel?.Tick(Time.unscaledDeltaTime, Rating.Mine, Rating.LocalClientId, Rating.CanRate, Rating.CopyTargetsTo, chinese, pointer);
    }

    /// <summary>The test toolbar's social buttons, while the split-screen preview runs.</summary>
    internal void OnPreviewAction(SplitScreenTestAction action)
    {
        if (_preview == null) return;
        switch (action)
        {
            case SplitScreenTestAction.AudienceEmote:
                int slot = _preview.RandomAudienceSlot();
                SplitScreenModule.Current?.ShowPreviewEmote(slot >= 0 ? slot : SplitScreenModule.PreviewLocalSlot,
                    SpectatorEmotes.Text(_preview.RandomEmote(), _config.UseChineseText));
                break;
            case SplitScreenTestAction.OpenEmotes: TogglePicker(); break;
            case SplitScreenTestAction.OpenBets: OpenBets(); break;
            case SplitScreenTestAction.OpenRatings: if (ReviewsOn) RatingPanel().Toggle(); break;
            case SplitScreenTestAction.CloseSocial:
                _picker?.Close(); _panel?.Close(); _ratingPanel?.Close(); _ratingPanel?.DismissSummary();
                break;
            case SplitScreenTestAction.EndRound:
                _ratingPanel?.Close();
                var summary = _preview.EndRound(Time.realtimeSinceStartupAsDouble, _previewResolved);
                foreach (var market in _previewResolved) ShowResult(market);
                if (summary.Count > 0) RatingPanel().ShowSummary(summary, _preview.NameOf, _config.UseChineseText);
                break;
        }
    }

    /// <summary>For the preview's demo: where a panel element is, in unzoomed screen pixels.</summary>
    internal Rect? DemoFocus(SplitScreenDemoTarget target, int a, int b) => target switch
    {
        SplitScreenDemoTarget.Emotes => _picker?.FocusPicker(),
        SplitScreenDemoTarget.Emote => _picker?.FocusEmote(a),
        SplitScreenDemoTarget.Bets => _panel?.FocusPanel(),
        SplitScreenDemoTarget.Stake => _panel?.FocusStake(a),
        SplitScreenDemoTarget.DeathMarket => _panel?.FocusDeathMarket(),
        SplitScreenDemoTarget.DeathOption => _panel?.FocusDeathOption(a),
        SplitScreenDemoTarget.Leaderboard => _panel?.FocusBoard(),
        SplitScreenDemoTarget.BetResult => _panel?.FocusResult(),
        SplitScreenDemoTarget.Reviews => _ratingPanel?.FocusPanel(),
        SplitScreenDemoTarget.ReviewRow => _ratingPanel?.FocusRow(a),
        SplitScreenDemoTarget.ReviewTag => _ratingPanel?.FocusChip(a, b),
        SplitScreenDemoTarget.Notice => _ratingPanel?.FocusSummary(a),
        _ => null
    };

    /// <summary>For the preview's demo: a click at an unzoomed point, where it is drawn now, as a player's would be.</summary>
    internal void DemoClick(Vector2 point) { if (_preview != null) Click(SpectatorDemoZoom.ToScreen(point)); }

    // The preview starts with the split-screen preview and ends with it; its panels close with it.
    private void TickPreview()
    {
        var split = SplitScreenModule.Current;
        if (split?.Preview != true)
        {
            if (_preview == null) return;
            // Nothing the preview produced reaches the real screen: its result cards and toast go with it.
            _preview = null; _participation.ResetPreview();
            _picker?.Close(); _panel?.Close(); _panel?.ClearResults(); _ratingPanel?.Close(); _ratingPanel?.DismissSummary();
            return;
        }
        _preview ??= new SocialPreview(_config.UseChineseText);
        split.CopyPreviewPeopleTo(_previewPeople);
        _preview.Update(_previewPeople, Time.realtimeSinceStartupAsDouble, _previewResolved);
        foreach (var market in _previewResolved) ShowResult(market);
    }

    private void Click(Vector2 point)
    {
        bool chinese = _config.UseChineseText;
        if (_panel?.IsOpen == true)
        {
            var hit = _panel.HitTest(point, out int market, out int value);
            if (hit == SpectatorBettingPanel.Hit.Stake) _panel.SelectStake(value);
            else if (hit == SpectatorBettingPanel.Hit.Option && Book.Find(market) is { } chosen)
            {
                if (_preview != null) _preview.PlaceBet(market, value, _panel.Stake, Time.realtimeSinceStartupAsDouble);
                else Betting.PlaceBet(market, value, _panel.Stake);
                _panel.Toast((chinese ? $"已押 {_panel.Stake} 分 · " : $"Staked {_panel.Stake} · ")
                    + SpectatorBettingPanel.OptionLabel(chosen, value, NameOf, chinese));
            }
            else if (hit == SpectatorBettingPanel.Hit.OptOut)
            {
                if (!_panel.ConfirmingOptOut) _panel.ArmOptOut();
                else
                {
                    _participation.Skip(_preview != null, Session); _panel.Close();
                    _panel.ShowNotice(chinese ? "本局不再参与竞猜" : "Bets skipped for this game",
                        chinese ? "回到大厅开始新的一局后恢复" : "They return with the next game from the lobby");
                }
            }
        }
        if (_ratingPanel?.IsOpen == true && _ratingPanel.HitTest(point, out ulong target, out int tag)
            && (_preview != null ? _preview.Rate(target, tag) : Rating.Toggle(target, tag)))
            _ratingPanel.Refresh();
        if (_picker?.IsOpen == true && _picker.HitTest(point) is var emote && emote >= 0) Send(emote);
    }

    private void OpenBets()
    {
        bool chinese = _config.UseChineseText;
        // Without this mod on the host there is nothing to open; with it, say who turns bets on.
        if (!BetsOn)
        {
            if (_preview != null || Network.Ready)
                Panel().ShowNotice(chinese ? "观众竞猜未开启" : "Audience bets are off",
                    _preview != null || Network.IsHost ? (chinese ? "可在 ESC → 增强观战 → 选项 → 界面与声音 中开启" : "Turn them on in ESC → Enhanced Spectator → Options → Interface")
                        : (chinese ? "由房主在选项中开启" : "The host turns them on in the options"));
            return;
        }
        var panel = Panel();
        if (OptedOut) panel.ShowNotice(chinese ? "本局已选择不参与竞猜" : "You skipped bets this game",
            chinese ? "回到大厅开始新的一局后可重新参与" : "They return when a new game starts from the lobby");
        else panel.Toggle();
    }

    private void TogglePicker()
    {
        if (_picker?.IsOpen == true) { _picker.Close(); return; }
        _picker ??= new SpectatorEmotePicker(SpectatorEmotes.Count);
        _picker.ConfigureFont(LethalCompanySplitScreenState.Font);
        bool chinese = _config.UseChineseText;
        _picker.Open(i => SpectatorEmotes.Text(i, chinese), SplitScreenModule.Current?.AudienceHeight ?? 0);
    }

    /// <summary>
    /// A result card for each decided market you bet on, and for each answered one while you watch from the
    /// audience; an undecided refund you had no part in shows nothing, and skipping bets hides them all.
    /// </summary>
    internal static bool ShowsResult(BetMarket market, BetEntry? mine, bool audience, bool optedOut)
        => !optedOut && (mine != null || audience && market.Winners.Count > 0);

    private void ShowResult(BetMarket market)
    {
        // The demo shows results only in its bets chapter.
        if (!BetsOn || SplitScreenModule.Current?.DemoHidesBetResults == true) return;
        var mine = market.BetOf(LocalId);
        if (!ShowsResult(market, mine, _preview != null || Betting.LocalDead, OptedOut)) return;
        bool chinese = _config.UseChineseText;
        string answer = SpectatorBettingPanel.Answer(market, NameOf, chinese);
        string? footer = mine is { Late: true } ? chinese ? $"结算前 {BettingState.LateSeconds:0} 秒内的押注不计，已退还" : $"Bets in the last {BettingState.LateSeconds:0}s don't count: refunded"
            : null;
        Panel().ShowResult(SpectatorBettingPanel.Title(market.Kind, chinese), answer, mine != null ? mine.Payout - mine.Stake : 0, mine != null, footer);
    }

    private SpectatorBettingPanel Panel()
    {
        _panel ??= new SpectatorBettingPanel();
        _panel.ConfigureFont(LethalCompanySplitScreenState.Font);
        return _panel;
    }

    private SpectatorRatingPanel RatingPanel()
    {
        _ratingPanel ??= new SpectatorRatingPanel();
        _ratingPanel.ConfigureFont(LethalCompanySplitScreenState.Font);
        return _ratingPanel;
    }

    private void OnRatingSummary(List<(ulong Target, int[] Counts)> summary)
    {
        if (summary.Count > 0) RatingPanel().ShowSummary(summary, Rating.NameOf, _config.UseChineseText);
    }

    private void Send(int emote)
    {
        if (_preview != null)
        {
            // The preview's own emotes stay on this screen, above the local audience seat, with the usual cooldown.
            if (EmoteCooldown > 0) { _picker!.Touch(); return; }
            _previewCooldownUntil = Time.realtimeSinceStartup + SpectatorEmotes.CooldownSeconds;
            SplitScreenModule.Current?.ShowPreviewEmote(SplitScreenModule.PreviewLocalSlot, SpectatorEmotes.Text(emote, _config.UseChineseText));
            _picker!.Sent(emote);
            return;
        }
        if (Network.SendEmote(emote)) _picker!.Sent(emote);
        else _picker!.Touch();
    }

    private void OnEmote(ulong sender, int emote)
    {
        if (sender != Network.LocalClientId && !_config.Camera.ShowEmotes.Value) return;
        SpectatorSocialEvents.RaiseEmote(sender, SpectatorEmotes.Text(emote, _config.UseChineseText));
    }

    public void Dispose()
    {
        Network.EmoteReceived -= OnEmote;
        Betting.Resolved -= ShowResult;
        Rating.Summary -= OnRatingSummary;
        Chat.Dispose(); Network.Dispose(); _picker?.Dispose(); _picker = null; _panel?.Dispose(); _panel = null; _ratingPanel?.Dispose(); _ratingPanel = null;
        if (Current == this) Current = null;
    }
}

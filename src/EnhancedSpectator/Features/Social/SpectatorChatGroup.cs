using System;
using System.Collections.Generic;
using EnhancedSpectator.Config;
using EnhancedSpectator.Features.Spectator;
using EnhancedSpectator.Features.SplitScreen;
using EnhancedSpectator.GameInterop;
using UnityEngine;

namespace EnhancedSpectator.Features.Social;

/// <summary>
/// The spectator chat group, on this mod's channel (the host must run it): dead players talk there, and the living
/// with the mod read it in the game's chat box. The living chat as the game has them (the dead read every living
/// player's lines anyway). A dead player's own chat panel stands in for the game's chat box: in the split-screen, or
/// in the corner of a single spectator view.
/// </summary>
internal sealed class SpectatorChatGroup : ISpectatorChatGroup, IDisposable
{
    private readonly EnhancedSpectatorConfig _config;
    private readonly SocialNetworkService _network;
    private readonly List<string> _history = new List<string>();
    private SpectatorChatOverlay? _overlay;
    private int _seen = -1;

    internal SpectatorChatGroup(EnhancedSpectatorConfig config, SocialNetworkService network)
    {
        _config = config; _network = network;
        network.ChatReceived += OnChat;
        LethalCompanyChat.Group = this;
    }

    public bool Enabled => _config.EnableEnhancedSpectator.Value;
    public bool Available => Enabled && _network.Ready;
    public void Send(string text) => _network.SendChat(text);

    private void OnChat(ulong sender, string text)
    { if (Enabled && LethalCompanyChat.InGame) LethalCompanyChat.ShowGroup(_network.Game.NameOf(sender), text, _config.UseChineseText); }

    internal void Tick()
    {
        if (!Enabled || !LethalCompanyChat.InGame)
        {
            LethalCompanyChat.CancelLineReopen();
            LethalCompanyChat.VanillaHidden = false;
            _overlay?.Hide(); return;
        }
        bool dead = _network.Game.LocalDead, chinese = _config.UseChineseText;
        // Menus/reports use the native chat. Never hide it while its replacement is hidden or covered.
        bool native = LethalCompanyChat.MenuOpen || LethalCompanyRoundResults.Open || LethalCompanySpectatorUiVisibility.Hidden;
        var split = SplitScreenModule.Current;
        LethalCompanyChat.VanillaHidden = dead && !native && (split?.Active != true || split.PresentsChat);
        LethalCompanyChat.KeepLineOpen();
        bool typing = LethalCompanyChat.Typing;
        if (dead && typing) LethalCompanyChat.DropTabs();
        // A single spectator view has the panel in the game's chat corner; the split-screen has its own.
        bool single = dead && split?.Active != true && !native;
        if (!single && _overlay == null) return;
        _seen = LethalCompanyChat.Read(_history, _seen);
        if (single && _overlay == null) { _overlay = new SpectatorChatOverlay(); _overlay.ConfigureFont(LethalCompanySplitScreenState.Font); }
        _overlay!.MatchChatText(LethalCompanyChat.MessagesText, LethalCompanyChat.LineText);
        _overlay!.Tick(single, typing, typing ? LethalCompanyChat.Input : string.Empty, _history, _seen, _config.Camera.SplitScreen.ChatPopup.Value, chinese,
            Time.unscaledDeltaTime,
            typing ? LethalCompanyChat.Caret : -1, typing ? LethalCompanyChat.Composing : string.Empty);
    }

    public void Dispose()
    {
        _network.ChatReceived -= OnChat;
        if (ReferenceEquals(LethalCompanyChat.Group, this))
        { LethalCompanyChat.CancelLineReopen(); LethalCompanyChat.Group = null; }
        LethalCompanyChat.VanillaHidden = false;
        _overlay?.Dispose();
    }
}

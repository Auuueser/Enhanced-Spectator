using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EnhancedSpectator.GameInterop;

/// <summary>The spectator chat group as the game's chat sees it (the social module provides it).</summary>
internal interface ISpectatorChatGroup
{
    /// <summary>The enhanced spectator feature is enabled, independently of host compatibility.</summary>
    bool Enabled { get; }
    /// <summary>Lines can be sent: the host runs this mod.</summary>
    bool Available { get; }
    void Send(string text);
}

/// <summary>
/// The game's chat: the message history the HUD keeps (formatted, its colour tags included) and the line being typed,
/// which the spectator chat panels show, and the hooks of the spectator chat group. A dead player's lines go to the
/// group instead of the game; group lines join the same history, so the living read them in the game's chat box (and
/// the dead read the living's lines there too, so the living need no channel of their own). NiceChat keeps its longer history and its input in the same places.
/// </summary>
internal static class LethalCompanyChat
{
    private static int _count = -1, _version;
    private static string? _last;
    private static Keyboard? _imeKeyboard;
    private static string _composing = string.Empty;
    private static int _reopenAfter = -1;
    private static TMP_InputField? _reopenField;

    internal static ISpectatorChatGroup? Group;
    /// <summary>The spectator's own chat panel stands in for the game's chat box, which then does not draw.</summary>
    internal static bool VanillaHidden;
    /// <summary>The split-screen preview's own panel shows the chat (its tester is usually alive).</summary>
    internal static bool PreviewPresents;
    // One of our chat panels shows the line: it is opened and closed by hand.
    private static bool PanelOwnsLine => VanillaHidden || PreviewPresents;

    internal static string Input => HUDManager.Instance.chatTextField.text;
    /// <summary>The game's chat texts (its messages, and the line being typed), as other mods may have dressed them.</summary>
    internal static TMP_Text MessagesText => HUDManager.Instance.chatText;
    internal static TMP_Text LineText => HUDManager.Instance.chatTextField.textComponent;
    /// <summary>Where the caret is in <see cref="Input"/>.</summary>
    internal static int Caret => Mathf.Clamp(HUDManager.Instance.chatTextField.stringPosition, 0, Input.Length);

    /// <summary>
    /// What an input method (pinyin and the like) is composing, before it becomes text in the line. The game runs the
    /// Input System only, so the chat field gets the committed characters but not this; the keyboard reports it.
    /// </summary>
    internal static string Composing
    {
        get
        {
            var keyboard = Keyboard.current;
            if (keyboard != _imeKeyboard)
            {
                if (_imeKeyboard != null) _imeKeyboard.onIMECompositionChange -= OnComposition;
                _imeKeyboard = keyboard; _composing = string.Empty;
                if (keyboard != null) keyboard.onIMECompositionChange += OnComposition;
            }
            return _composing;
        }
    }
    private static void OnComposition(IMECompositionString composition) => _composing = composition.ToString();
    /// <summary>In a game, with its HUD and the local player.</summary>
    internal static bool InGame => HUDManager.Instance != null && StartOfRound.Instance != null && StartOfRound.Instance.localPlayerController != null;
    /// <summary>The local player is typing a chat line (alive or dead).</summary>
    internal static bool Typing => StartOfRound.Instance.localPlayerController.isTypingChat;
    /// <summary>The ESC menu is open.</summary>
    internal static bool MenuOpen => StartOfRound.Instance.localPlayerController.quickMenuManager.isMenuOpen;

    /// <summary>The history's version; when it differs from <paramref name="had"/>, the history is copied into <paramref name="copy"/>.</summary>
    internal static int Read(List<string> copy, int had)
    {
        var history = HUDManager.Instance.ChatMessageHistory;
        string? last = history.Count > 0 ? history[history.Count - 1] : null;
        if (history.Count != _count || !ReferenceEquals(last, _last)) { _count = history.Count; _last = last; _version++; }
        if (had != _version) { copy.Clear(); copy.AddRange(history); }
        return _version;
    }

    /// <summary>
    /// SubmitChat_performed, before the game: a dead player's line goes to the group (a living player's stays the
    /// game's). False leaves the line to the game. A line starting with "/" always stays with the game and the
    /// chat-command mods (ChatCommandAPI and others), whatever their order. A spectator's own chat panel is opened and
    /// closed by hand: after a line it stays open for the next one, and an empty line closes it (as Esc does); the game's
    /// chat box (the living's) closes after each line as the game closes it.
    /// </summary>
    internal static bool SubmitToGroup(HUDManager hud)
    {
        var player = GameNetworkManager.Instance.localPlayerController;
        string text = hud.chatTextField.text;
        if (Group is not { Available: true } group || player == null || !player.isTypingChat || !player.isPlayerDead
            || text.TrimStart().StartsWith('/')) return false;
        bool line = text.Trim().Length > 0;
        if (line) group.Send(text);
        if (line && PanelOwnsLine)
        { hud.chatTextField.text = ""; _composing = string.Empty; QueueLineReopen(hud.chatTextField); }
        else Close(hud, player);
        return true;
    }

    /// <summary>
    /// The line kept open after a send: the field ends its edit later in that frame (after the submit action), so it
    /// starts again on the next one.
    /// </summary>
    internal static void KeepLineOpen()
    {
        if (_reopenAfter < 0 || Time.frameCount <= _reopenAfter) return;
        var field = _reopenField;
        CancelLineReopen();
        var player = StartOfRound.Instance.localPlayerController;
        if (field == null || field != HUDManager.Instance.chatTextField || !player.isTypingChat || !CanUseLine(field)) return;
        // The deferred submit repair belongs to this field. A menu or a deliberate selection elsewhere wins.
        if (!field.isFocused) { field.Select(); field.ActivateInputField(); }
    }

    private static void QueueLineReopen(TMP_InputField field) { _reopenField = field; _reopenAfter = Time.frameCount; }
    internal static void CancelLineReopen() { _reopenField = null; _reopenAfter = -1; }

    /// <summary>
    /// SubmitChat_performed, after the game: a dead player's line the game ignored (a command no mod took) closes as a
    /// sent one would; a line the game sent and closed stays open when one of our panels shows it (the preview's too).
    /// </summary>
    internal static void AfterSubmit(HUDManager hud, bool hadLine)
    {
        var player = GameNetworkManager.Instance.localPlayerController;
        if (player == null || Group is not { Enabled: true }) return;
        if (player.isPlayerDead && player.isTypingChat) Close(hud, player);
        else if (hadLine && PanelOwnsLine && !player.isTypingChat) { player.isTypingChat = true; QueueLineReopen(hud.chatTextField); }
    }

    private static bool MenuBlocksChat(GameNetcodeStuff.PlayerControllerB player)
        => player.quickMenuManager.isMenuOpen || player.inTerminalMenu;

    private static bool CanUseLine(TMP_InputField field)
    {
        var events = EventSystem.current;
        return !MenuBlocksChat(StartOfRound.Instance.localPlayerController) && events != null
            && (events.currentSelectedGameObject == null || events.currentSelectedGameObject == field.gameObject);
    }

    private static void Close(HUDManager hud, GameNetcodeStuff.PlayerControllerB player)
    {
        CancelLineReopen();
        player.isTypingChat = false;
        hud.chatTextField.text = ""; _composing = string.Empty;
        EventSystem.current.SetSelectedGameObject(null);
        hud.PingHUDElement(hud.Chat);
        hud.typingIndicator.enabled = false;
    }

    /// <summary>EnableChat_performed, after the game: the game opens the chat only for the living; a dead player types to the group.</summary>
    internal static void OpenForDead()
    {
        var player = GameNetworkManager.Instance.localPlayerController;
        if (Group is not { Available: true } || player == null || !player.isPlayerDead || player.isTypingChat || MenuBlocksChat(player)) return;
        player.isTypingChat = true;
        var hud = HUDManager.Instance;
        hud.chatTextField.Select();
        hud.PingHUDElement(hud.Chat, .1f, 1f, 1f);
        hud.typingIndicator.enabled = true;
    }

    /// <summary>Whether a received line should be watched: the game shows a living player's lines only to the living.</summary>
    internal struct Heard { internal bool Watch; internal int Count; internal string? Last; }

    /// <summary>AddPlayerChatMessageClientRpc as it runs on this client, before the game: a dead player watches a living player's line.</summary>
    internal static Heard BeforeHeard(HUDManager hud, int playerId)
    {
        var players = StartOfRound.Instance.allPlayerScripts;
        var local = GameNetworkManager.Instance.localPlayerController;
        if (Group is not { Enabled: true } || hud.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || local == null || !local.isPlayerDead
            || playerId < 0 || playerId >= players.Length || players[playerId].isPlayerDead) return default;
        var history = hud.ChatMessageHistory;
        return new Heard { Watch = true, Count = history.Count, Last = history.Count > 0 ? history[history.Count - 1] : null };
    }

    /// <summary>After the game: the line reaches the spectator too, unless the call showed it already (NiceChat lets the dead read everything).</summary>
    internal static void AfterHeard(HUDManager hud, string chatMessage, int playerId, Heard heard)
    {
        var history = hud.ChatMessageHistory;
        if (!heard.Watch || history.Count != heard.Count || !ReferenceEquals(history.Count > 0 ? history[history.Count - 1] : null, heard.Last)) return;
        hud.AddChatMessage(chatMessage, StartOfRound.Instance.allPlayerScripts[playerId].playerUsername, playerId);
    }

    /// <summary>A spectator group line, joining the history like any other: a grey "[观战] name", the text as typed (no markup).</summary>
    internal static void ShowGroup(string name, string text, bool chinese)
        => HUDManager.Instance.AddChatMessage(NoMarkup(text), "<color=#A6A6A6>" + (chinese ? "[观战] " : "[Spectators] ") + NoMarkup(name) + "</color>");

    private static string NoMarkup(string text) => "<noparse>" + text.Replace("</noparse>", "</ noparse>") + "</noparse>";

    /// <summary>Tab characters the chat field took (a spectator's panel has no use for them: they showed as a wide gap).</summary>
    internal static void DropTabs()
    {
        var field = HUDManager.Instance.chatTextField;
        if (field.text.IndexOf('\t') >= 0) field.text = field.text.Replace("\t", "");
    }

    /// <summary>The game's chat box (its corner: background, messages and line) and its typing mark, which a spectator's panel replaces.</summary>
    internal static Transform VanillaBox(HUDManager hud) => hud.HUDContainer.transform.Find("BottomLeftCorner");
}

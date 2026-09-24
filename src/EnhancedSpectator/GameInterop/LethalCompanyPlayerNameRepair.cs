using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using Steamworks;

namespace EnhancedSpectator.GameInterop;

// Post-synchronization repair avoids changing RPC payloads or competing transpilers.
internal sealed class LethalCompanyPlayerNameRepair : IDisposable
{
    private const string ChineseMemberNameKey = "Aueser.LCChineseProject.player-name.v1";
    private sealed class SavedName
    {
        internal ulong SteamId, ClientId, SlotId;
        internal string Raw = "", PreviousRaw = "", PersonaFallback = "";
        internal readonly Dictionary<string, PlayerNameOwnedWrite> Writes = new Dictionary<string, PlayerNameOwnedWrite>();
    }
    private static bool MatchesIdentity(PlayerControllerB player, SavedName state) => player != null
        && player.playerSteamId == state.SteamId && player.actualClientId == state.ClientId
        && player.playerClientId == state.SlotId && !player.disconnectedMidGame;
    private readonly Dictionary<PlayerControllerB, SavedName> _saved = new Dictionary<PlayerControllerB, SavedName>();
    private static LethalCompanyPlayerNameRepair? _active;

    internal static bool TryGetVerifiedDisplayName(ulong clientId, ulong slotId, out string name)
    {
        name = string.Empty;
        if (_active == null || StartOfRound.Instance == null || !SteamClient.IsValid) return false;
        foreach (var pair in _active._saved)
        {
            var player = pair.Key;
            if (player == null || player.actualClientId != clientId || player.playerClientId != slotId
                || !MatchesIdentity(player, pair.Value) || (!player.isPlayerControlled && !player.isPlayerDead)) continue;
            // Recheck the current name so another mod's later custom nickname always wins.
            return PlayerNameRepairRules.TryRepair(player.playerUsername, pair.Value.Raw, out name);
        }
        return false;
    }

    internal void Refresh(bool enabled)
    {
        if (!enabled) { Dispose(); return; }
        var round = StartOfRound.Instance;
        if (round == null || round.allPlayerScripts == null || !SteamClient.IsValid
            || GameNetworkManager.Instance == null || GameNetworkManager.Instance.disableSteam) { Dispose(); return; }
        _active = this;
        var stale = new List<PlayerControllerB>();
        foreach (var pair in _saved)
            if (!MatchesIdentity(pair.Key, pair.Value) || (!pair.Key.isPlayerControlled && !pair.Key.isPlayerDead)
                || Array.IndexOf(round.allPlayerScripts, pair.Key) < 0) stale.Add(pair.Key!);
        foreach (var player in stale) _saved.Remove(player);
        foreach (var player in round.allPlayerScripts)
        {
            if (player == null || player.playerSteamId == 0 || player.disconnectedMidGame || (!player.isPlayerControlled && !player.isPlayerDead)) continue;
            if (!_saved.TryGetValue(player, out var state))
            {
                state = new SavedName { SteamId = player.playerSteamId, ClientId = player.actualClientId, SlotId = player.playerClientId };
                _saved[player] = state;
                if (player.playerSteamId != (ulong)SteamClient.SteamId) SteamFriends.RequestUserInformation(player.playerSteamId, true);
            }
            string raw = ReadSource(player.playerSteamId, out string persona);
            if (PlayerNameRepairRules.Sanitize(raw, true).Length == 0) continue;
            state.PreviousRaw = state.Raw; state.Raw = raw; state.PersonaFallback = persona;
            bool recognized = PlayerNameRepairRules.TryRepair(player.playerUsername, raw, out string name);
            if (!recognized && !PlayerNameRepairRules.CanRepairSurface(player.playerUsername, state.PreviousRaw)
                && !PlayerNameRepairRules.CanRepairSurface(player.playerUsername, persona)) continue;
            Write("username", player, () => player != null ? player.playerUsername : null, value => { if (player != null) player.playerUsername = value; }, name, state);
            var billboard = player.usernameBillboardText;
            if (billboard != null) Write("billboard", billboard, () => billboard != null ? billboard.text : null, value => { if (billboard != null) billboard.text = value; }, name, state);
            int slot = Array.IndexOf(round.allPlayerScripts, player);
            var targets = round.mapScreen != null ? round.mapScreen.radarTargets : null;
            if (targets != null)
                foreach (var target in targets)
                    if (target != null && !target.isNonPlayer && target.transform == player.transform)
                    { Write("radar", target, () => target.name, value => target.name = value, name, state); break; }
            var menu = player.quickMenuManager;
            if (menu != null && menu.playerListSlots != null && slot >= 0 && slot < menu.playerListSlots.Length)
            {
                var label = menu.playerListSlots[slot]?.usernameHeader;
                if (label != null) Write("menu", label, () => label != null ? label.text : null, value => { if (label != null) label.text = value; }, name, state);
            }
        }
    }

    private static string ReadSource(ulong steamId, out string persona)
    {
        persona = steamId == (ulong)SteamClient.SteamId ? SteamClient.Name : new Friend(steamId).Name;
        if (steamId == (ulong)SteamClient.SteamId) return persona;
        var lobby = GameNetworkManager.Instance.currentLobby;
        if (lobby.HasValue)
        {
            string advertised = PlayerNameRepairRules.Advertised(lobby.Value.GetMemberData(new Friend(steamId), ChineseMemberNameKey));
            if (advertised.Length > 0) return advertised;
        }
        return persona;
    }

    private static void Write(string key, object target, Func<string?> read, Action<string> write, string name, SavedName state)
    {
        if (!state.Writes.TryGetValue(key, out var saved)) state.Writes[key] = saved = new PlayerNameOwnedWrite();
        string? current = read();
        saved.Apply(target, read, write, name, current != null
            && (PlayerNameRepairRules.CanRepairSurface(current, state.Raw)
                || PlayerNameRepairRules.CanRepairSurface(current, state.PreviousRaw)
                || PlayerNameRepairRules.CanRepairSurface(current, state.PersonaFallback)));
    }

    public void Dispose()
    {
        if (ReferenceEquals(_active, this)) _active = null;
        foreach (var pair in _saved)
        {
            if (!MatchesIdentity(pair.Key, pair.Value)) continue;
            foreach (var saved in pair.Value.Writes.Values)
                saved.Restore();
        }
        _saved.Clear();
    }
}

using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The one name every Enhanced Spectator display shows for a player (split-screen views, audience row, ghost name
/// tags, watch panel, bets, ratings). Display text is never a source: it reads the verified Steam name when the
/// name repair has one, otherwise the game's name, cleans it and keeps it stable per player identity.
/// </summary>
internal static class PlayerDisplayNames
{
    private static readonly PlayerNameStabilizer Stabilizer = new PlayerNameStabilizer();
    // Cleaning builds a new string; names are read every frame for every view, so each player's last raw name and
    // its cleaned form are kept and the cleaning only runs again when the raw name (or the slot) changes.
    private static readonly Dictionary<ulong, (string Raw, ulong Slot, string Clean)> Cleaned = new Dictionary<ulong, (string, ulong, string)>();

    internal static string Of(PlayerControllerB player)
    {
        string raw = (LethalCompanyPlayerNameRepair.TryGetVerifiedDisplayName(player.actualClientId, player.playerClientId, out string verified)
            ? verified : player.playerUsername) ?? string.Empty;
        if (!Cleaned.TryGetValue(player.actualClientId, out var last) || last.Raw != raw || last.Slot != player.playerClientId)
        {
            string clean = PlayerNameText.Clean(raw);
            last = (raw, player.playerClientId, clean.Length > 0 ? clean : "Player #" + (player.playerClientId + 1));
            Cleaned[player.actualClientId] = last;
        }
        return Stabilizer.Resolve(player.actualClientId, player.playerSteamId, last.Clean, Time.unscaledTime);
    }

    internal static void Clear() { Stabilizer.Clear(); Cleaned.Clear(); }
}

/// <summary>Removes what renders badly or invisibly in a name, keeping every real letter, digit, symbol and space.</summary>
internal static class PlayerNameText
{
    internal const int MaxLength = 32;

    /// <summary>
    /// Drops control, format (zero-width, direction marks) and private-use characters; turns other white space
    /// (full-width, no-break, tabs, line breaks) into ordinary spaces; trims the ends; caps length. Spaces inside a
    /// name stay exactly as written, as LC Chinese Project keeps them: "汉␣␣␣␣2" shows as written, so every display
    /// matches the head billboard and ESC list. A name of only spaces or invisible characters becomes empty.
    /// </summary>
    internal static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var builder = new StringBuilder(raw!.Length);
        for (int i = 0; i < raw.Length && builder.Length < MaxLength; i++)
        {
            char c = raw[i];
            if (char.IsHighSurrogate(c) && i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1]))
            { builder.Append(c).Append(raw[++i]); continue; }
            var category = char.GetUnicodeCategory(c);
            if (char.IsWhiteSpace(c) || category == System.Globalization.UnicodeCategory.SpaceSeparator) { if (builder.Length > 0) builder.Append(' '); continue; }
            if (category == System.Globalization.UnicodeCategory.Control || category == System.Globalization.UnicodeCategory.Format
                || category == System.Globalization.UnicodeCategory.PrivateUse || category == System.Globalization.UnicodeCategory.Surrogate
                || category == System.Globalization.UnicodeCategory.OtherNotAssigned) continue;
            builder.Append(c);
        }
        return builder.ToString().TrimEnd(' ');
    }
}

/// <summary>
/// Keeps a shown name steady while its source flickers (a mod and the game taking turns writing it): a different
/// name is adopted only after it has held for <see cref="SettleSeconds"/>. A new Steam identity resets the entry.
/// </summary>
internal sealed class PlayerNameStabilizer
{
    internal const float SettleSeconds = .6f;
    private sealed class Entry { internal ulong Identity; internal string Shown = string.Empty, Pending = string.Empty; internal float Since; }
    private readonly Dictionary<ulong, Entry> _entries = new Dictionary<ulong, Entry>();

    internal string Resolve(ulong key, ulong identity, string name, float now)
    {
        if (!_entries.TryGetValue(key, out var entry) || entry.Identity != identity)
        { _entries[key] = new Entry { Identity = identity, Shown = name }; return name; }
        if (name == entry.Shown) { entry.Pending = string.Empty; return name; }
        if (name != entry.Pending) { entry.Pending = name; entry.Since = now; }
        else if (now - entry.Since >= SettleSeconds) { entry.Shown = name; entry.Pending = string.Empty; }
        return entry.Shown;
    }

    internal void Clear() => _entries.Clear();
}

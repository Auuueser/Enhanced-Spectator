using System;

namespace EnhancedSpectator.Features.SplitScreen;

internal readonly struct SplitScreenKey : IEquatable<SplitScreenKey>
{
    internal readonly ulong ClientId, SlotId;
    internal SplitScreenKey(ulong clientId, ulong slotId) { ClientId = clientId; SlotId = slotId; }
    public bool Equals(SplitScreenKey other) => ClientId == other.ClientId && SlotId == other.SlotId;
    public override bool Equals(object? obj) => obj is SplitScreenKey other && Equals(other);
    public override int GetHashCode() => unchecked(ClientId.GetHashCode() * 397 ^ SlotId.GetHashCode());
    public static bool operator ==(SplitScreenKey a, SplitScreenKey b) => a.Equals(b);
    public static bool operator !=(SplitScreenKey a, SplitScreenKey b) => !a.Equals(b);
}

// Test identities are distinct from the real player supplying their picture.
internal readonly struct SplitScreenParticipant
{
    internal readonly SplitScreenKey Key, Source;
    internal readonly string Name;
    internal SplitScreenParticipant(SplitScreenKey key, SplitScreenKey source, string name)
    { Key = key; Source = source; Name = name; }
}

// A dead player shown in the split-screen's dead-player bar. Source is the real player for voice and avatar.
internal readonly struct SplitScreenDeadPlayer
{
    internal readonly SplitScreenKey Key, Source;
    internal readonly string Name;
    internal readonly ulong SteamId;
    internal readonly bool Local;
    // Who this dead player is watching, when known (a mod peer's synced target); null otherwise.
    internal readonly SplitScreenKey? Watching;
    internal readonly string WatchingName;
    // Runs Enhanced Spectator (a handshaken mod peer, the local player or a preview identity).
    internal readonly bool Enhanced;
    // Split-screen audience or watching (None: single view, or no mod). Only split-screen players can be followed.
    internal readonly Networking.SpectatorSplitView SplitView;
    // The audience member they watch together with, by name; empty when not following.
    internal readonly string FollowingName;
    // They follow us directly (FollowingName is ours), or their follow chain leads back to us: following them would loop.
    internal readonly bool FollowingYou, FollowLoop;
    internal SplitScreenDeadPlayer(SplitScreenKey key, SplitScreenKey source, string name, ulong steamId, bool local,
        SplitScreenKey? watching = null, string watchingName = "", bool enhanced = true,
        Networking.SpectatorSplitView splitView = Networking.SpectatorSplitView.Watching, string followingName = "",
        bool followingYou = false, bool followLoop = false)
    {
        Key = key; Source = source; Name = name; SteamId = steamId; Local = local; Watching = watching; WatchingName = watchingName;
        Enhanced = enhanced; SplitView = splitView; FollowingName = followingName; FollowingYou = followingYou; FollowLoop = followLoop;
    }
}

/// <summary>How strongly a speaker lights up: a quiet voice glows faintly, a loud one fully.</summary>
internal static class SplitScreenVoiceLevel
{
    internal const float Quiet = .3f;

    /// <summary>
    /// 0 when silent; otherwise from <see cref="Quiet"/> up to 1 with the voice amplitude. Speech amplitudes are
    /// small (around 0.02–0.25), so a square root spreads them across the range.
    /// </summary>
    internal static float Intensity(bool speaking, float amplitude)
    {
        if (!speaking) return 0;
        float loud = amplitude <= 0 ? 0 : (float)Math.Sqrt(Math.Min(1f, amplitude * 4));
        return Quiet + (1 - Quiet) * loud;
    }
}

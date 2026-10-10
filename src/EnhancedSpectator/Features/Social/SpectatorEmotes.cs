namespace EnhancedSpectator.Features.Social;

/// <summary>Short text emotes; only their index travels over the network, each client shows its own language.</summary>
internal static class SpectatorEmotes
{
    internal const int Count = 6;
    /// <summary>Per-sender minimum interval, enforced when sending and again when receiving.</summary>
    internal const float CooldownSeconds = 3f;
    // Same meaning per slot, phrased as each language's players say it: laugh, praise, cheer, warning, confusion,
    // and "that's it, we're done".
    private static readonly string[] Chinese = { "哈哈哈", "666", "冲！", "小心！", "？？？", "寄了" },
        English = { "LOL", "Nice!", "Go go go!", "Watch out!", "???", "GG" };
    internal static bool Valid(int id) => id >= 0 && id < Count;
    internal static string Text(int id, bool chinese) => (chinese ? Chinese : English)[id];
}

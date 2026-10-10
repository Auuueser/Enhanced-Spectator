namespace EnhancedSpectator.Features.Spectator;

/// <summary>The thermal colour schemes, in their cycling order (options page, ↑/↓ while thermal is on, key hints).</summary>
internal static class ThermalPalettes
{
    internal const int Count = 3;
    private static readonly string[] Chinese = { "铁红", "白热", "彩虹" }, English = { "Ironbow", "White hot", "Rainbow" };
    internal static string Name(int palette, bool chinese) => (chinese ? Chinese : English)[((palette % Count) + Count) % Count];
    internal static int Cycle(int palette, int direction) => ((palette + direction) % Count + Count) % Count;
}

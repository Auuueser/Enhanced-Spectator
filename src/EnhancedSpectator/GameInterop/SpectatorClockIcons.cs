using UnityEngine;

namespace EnhancedSpectator.GameInterop;

/// <summary>
/// The game's dawn, noon and sundown clock icons (sun, half sun, moon) hand-drawn at 24x22 for small sizes, after its
/// 53x48 originals: those are 1-pixel line art whose lines an automatic shrink merges at about 22 pixels. Indexed by
/// the game's DayMode; the midnight skull is not here (its automatic redraw is the one chosen).
/// </summary>
internal static class SpectatorClockIcons
{
    internal const int Width = 24, Height = 22;
    // '#' lit, '.' clear; the first row is the top.
    private static readonly string[][] Art =
    {
        new[]
        {
            "...........#............",
            "...........#............",
            "...#.......#.......#....",
            "....#......#......#.....",
            ".....#...........#......",
            ".........#####..........",
            ".......##.....##........",
            ".......#.......#........",
            "......#.........#.......",
            "......#.........#.......",
            ".####.#.........#.####..",
            "......#.........#.......",
            "......#.........#.......",
            ".......#.......#........",
            ".......##.....##........",
            ".........#####..........",
            ".....#...........#......",
            "....#......#......#.....",
            "...#.......#.......#....",
            "...........#............",
            "...........#............",
            "........................",
        },
        new[]
        {
            "........................",
            "........................",
            "........................",
            "...........#............",
            "...........#............",
            "...#.......#.......#....",
            "....#......#......#.....",
            ".....#...........#......",
            ".........#####..........",
            ".......##.....##........",
            ".......#.......#........",
            "......#.........#.......",
            "......#.........#.......",
            ".####.###########.####..",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        },
        new[]
        {
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "..........###...........",
            "........##..#...........",
            ".......#....#...........",
            ".......#....#...........",
            "......#.....#...........",
            "......#......#..........",
            "......#.......##........",
            "......#.........#.......",
            ".......#.......#........",
            "........##...##.........",
            "..........###...........",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        },
    };
    internal static int Count => Art.Length;
    private static readonly Texture2D?[] Made = new Texture2D?[3];

    /// <summary>The icon for day phase <paramref name="phase"/> (0 dawn to 2 sundown), white on clear, drawn 1:1.</summary>
    internal static Texture2D For(int phase)
    {
        if (Made[phase] is { } made) return made;
        var art = Art[phase]; var pixels = new Color32[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (art[Height - 1 - y][x] == '#') pixels[y * Width + x] = new Color32(255, 255, 255, 255);
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        { name = "EnhancedSpectator Clock Icon " + phase, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels32(pixels); texture.Apply(false, true);
        return Made[phase] = texture;
    }
}

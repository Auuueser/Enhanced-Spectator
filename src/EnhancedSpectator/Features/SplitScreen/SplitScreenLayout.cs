using System;

namespace EnhancedSpectator.Features.SplitScreen;

/// <summary>A video rectangle whose height always follows its 16:9 width.</summary>
internal readonly struct SplitScreenTileRect
{
    internal const float Aspect = 16f / 9f;
    internal readonly float CenterX, CenterY, Width;
    internal float Height => Width / Aspect;
    internal float Left => CenterX - Width * .5f;
    internal float Top => CenterY - Height * .5f;
    internal SplitScreenTileRect(float centerX, float centerY, float width)
    { CenterX = centerX; CenterY = centerY; Width = width; }
    internal bool Contains(float x, float y)
        => x >= Left && x <= Left + Width && y >= Top && y <= Top + Height;
}

/// <summary>Maximum-area equal video tiles with centered incomplete rows.</summary>
internal static class SplitScreenLayout
{
    internal const int MaximumWindows = 31;
    internal const float Margin = 16f, Gap = 8f;

    // reservedLeft: width kept free on the left (the split-screen chat panel), a gap from the views.
    internal static SplitScreenTileRect[] Calculate(int count, float width, float height, int focusIndex = -1, float reservedTop = 0, float reservedBottom = 0,
        float reservedLeft = 0)
    {
        var result = new SplitScreenTileRect[count];
        if (count == 0) return result;
        float left = reservedLeft > 0 ? reservedLeft + Gap : 0;
        float x = Margin + left, y = Margin + reservedTop;
        float availableWidth = width - Margin * 2 - left, availableHeight = height - Margin * 2 - reservedTop - reservedBottom;
        if (focusIndex < 0 || count == 1)
        { FillGrid(result, count, x, y, availableWidth, availableHeight, -1); return result; }

        float mainWidth = (availableWidth - Gap) * .7f;
        float mainVideoWidth = Math.Min(mainWidth, availableHeight * SplitScreenTileRect.Aspect);
        result[focusIndex] = new SplitScreenTileRect(x + mainWidth * .5f, y + availableHeight * .5f, mainVideoWidth);
        FillGrid(result, count - 1, x + mainWidth + Gap, y, availableWidth - mainWidth - Gap, availableHeight, focusIndex);
        return result;
    }

    private static void FillGrid(SplitScreenTileRect[] result, int count, float x, float y, float width, float height, int skip)
    {
        int columns = 1, rows = count;
        float tileWidth = 0;
        for (int candidate = 1; candidate <= count; candidate++)
        {
            int candidateRows = (count + candidate - 1) / candidate;
            float size = Math.Min((width - (candidate - 1) * Gap) / candidate,
                (height - (candidateRows - 1) * Gap) / candidateRows * SplitScreenTileRect.Aspect);
            if (size > tileWidth + .001f || (Math.Abs(size - tileWidth) <= .001f && candidateRows < rows))
            { tileWidth = size; columns = candidate; rows = candidateRows; }
        }
        float tileHeight = tileWidth / SplitScreenTileRect.Aspect;
        float gridTop = y + (height - rows * tileHeight - (rows - 1) * Gap) * .5f;
        for (int index = 0; index < count; index++)
        {
            int row = index / columns, column = index % columns;
            int rowCount = Math.Min(columns, count - row * columns);
            float rowLeft = x + (width - rowCount * tileWidth - (rowCount - 1) * Gap) * .5f;
            int outputIndex = skip >= 0 && index >= skip ? index + 1 : index;
            result[outputIndex] = new SplitScreenTileRect(rowLeft + column * (tileWidth + Gap) + tileWidth * .5f,
                gridTop + row * (tileHeight + Gap) + tileHeight * .5f, tileWidth);
        }
    }
}

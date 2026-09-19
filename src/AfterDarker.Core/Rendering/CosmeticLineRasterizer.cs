namespace AfterDarker.Core.Rendering;

/// <summary>Generates the pixels of a one-pixel line before surface clipping.</summary>
/// <remarks>
/// The endpoint is excluded. Exact half-pixel ties go toward the top/left, matching our
/// modern Windows GDI oracle. This is measured compatibility, not a Windows 3.1 fidelity claim.
/// Keeping the original line endpoints preserves the rounding phase for off-screen lines.
/// See docs/runtime-code-map.md#drawing-state-and-pixels and LineRasterTests.
/// </remarks>
internal static class CosmeticLineRasterizer
{
    /// <summary>One integer pixel position before clipping.</summary>
    /// <param name="X">Horizontal coordinate; may lie outside the surface.</param>
    /// <param name="Y">Vertical coordinate; may lie outside the surface.</param>
    internal readonly record struct Pixel(int X, int Y);

    /// <summary>Step along the longer axis, rounding the shorter-axis displacement with the GDI tie rule.</summary>
    public static IEnumerable<Pixel> EnumeratePixels(short startX, short startY, short endX, short endY)
    {
        int horizontalDistance = Math.Abs((int)endX - startX);
        int verticalDistance = Math.Abs((int)endY - startY);
        int horizontalDirection = endX >= startX ? 1 : -1;
        int verticalDirection = endY >= startY ? 1 : -1;
        int majorAxisLength = Math.Max(horizontalDistance, verticalDistance);

        // Signed 16-bit endpoints bound this loop to 65,535 steps. A zero-length
        // line visits nothing, so neither branch divides by zero.
        for (int step = 0; step < majorAxisLength; step++)
        {
            if (horizontalDistance >= verticalDistance)
            {
                int column = startX + horizontalDirection * step;
                int row = startY + verticalDirection * MinorAxisDisplacement(
                    step, verticalDistance, horizontalDistance, verticalDirection);
                yield return new Pixel(column, row);
            }
            else
            {
                int row = startY + verticalDirection * step;
                int column = startX + horizontalDirection * MinorAxisDisplacement(
                    step, horizontalDistance, verticalDistance, horizontalDirection);
                yield return new Pixel(column, row);
            }
        }
    }

    /// <summary>Round toward the nearest pixel, resolving a halfway point toward smaller screen coordinates.</summary>
    private static int MinorAxisDisplacement(int step, int minorLength, int majorLength, int minorDirection)
    {
        // Moving positively requires a downward rounding bias at a tie. When moving
        // negatively, the same top/left rule requires the larger displacement instead.
        int halfPixelBias = (majorLength - (minorDirection > 0 ? 1 : 0)) / 2;
        long numerator = (long)step * minorLength + halfPixelBias;
        return (int)(numerator / majorLength);
    }
}

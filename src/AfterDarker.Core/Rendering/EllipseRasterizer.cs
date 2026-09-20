using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

/// <summary>Build clipped scanline spans for a solid ellipse without touching any device-context state.</summary>
/// <remarks>
/// One-pixel boundaries use integer ellipse stepping. Width two uses the band between
/// ellipses expanded and contracted by one pixel: a deterministic software stroke,
/// not a claim to reproduce every pixel of Windows' ellipse rasterizer at either width.
/// See docs/research/hard-rain-execution.md for the measured compatibility boundary.
/// Boundary stepping is adapted from Alois Zingl's MIT-licensed plotEllipseRect;
/// attribution and license: third-party/zingl-bresenham.txt.
/// </remarks>
internal static class EllipseRasterizer
{
    /// <summary>Inclusive horizontal intervals for the painted shape and its brush interior.</summary>
    internal sealed class Row
    {
        public int Left = int.MaxValue, Right = int.MinValue;
        public int BrushLeft = int.MinValue, BrushRight = int.MaxValue;
    }

    /// <summary>Rows retain original-curve rounding even when most of the ellipse is outside the surface.</summary>
    public static Row[] BuildRows(Rectangle16 rectangle, int penWidth, int surfaceHeight)
    {
        int left = Math.Min(rectangle.Left, rectangle.Right), right = Math.Max(rectangle.Left, rectangle.Right);
        int top = Math.Min(rectangle.Top, rectangle.Bottom), bottom = Math.Max(rectangle.Top, rectangle.Bottom);
        Row[] rows = NewRows(surfaceHeight);
        if (left == right || top == bottom) return rows;
        if (penWidth == 1)
        {
            TraceBoundary(left, top, right - 1, bottom - 1, rows);
            return rows;
        }

        // Center the two-pixel band on the original curve. Fill the inner ellipse
        // with the brush; do not paint over the outline in a second raster pass.
        TraceBoundary(left - 1, top - 1, right, bottom, rows);
        Row[] interior = NewRows(surfaceHeight);
        if (right - left > 2 && bottom - top > 2)
            TraceBoundary(left + 1, top + 1, right - 2, bottom - 2, interior);
        for (int row = 0; row < rows.Length; row++)
        {
            rows[row].BrushLeft = interior[row].Left;
            rows[row].BrushRight = interior[row].Right;
        }
        return rows;
    }

    private static Row[] NewRows(int height) => Enumerable.Range(0, height).Select(_ => new Row()).ToArray();

    /// <summary>
    /// Walk four symmetric boundary pixels per step using an implicit-ellipse error.
    /// Inclusive bounds preserve half-pixel centers for even dimensions. All squared
    /// arithmetic is 64-bit; signed Win16 endpoints bound the walk to O(width+height).
    /// </summary>
    private static void TraceBoundary(int left, int top, int right, int bottom, Row[] rows)
    {
        long diameterX = right - left, diameterY = bottom - top;
        long verticalParity = diameterY & 1;
        long horizontalError = 4 * (1 - diameterX) * diameterY * diameterY;
        long verticalError = 4 * (verticalParity + 1) * diameterX * diameterX;
        long error = horizontalError + verticalError + verticalParity * diameterX * diameterX;
        int lowerRow = top + (int)((diameterY + 1) / 2);
        int upperRow = lowerRow - (int)verticalParity;
        long horizontalIncrement = 8 * diameterY * diameterY;
        long verticalIncrement = 8 * diameterX * diameterX;
        do
        {
            RecordRow(upperRow, left, right);
            RecordRow(lowerRow, left, right);
            long doubledError = 2 * error;
            if (doubledError <= verticalError)
            {
                lowerRow++; upperRow--;
                verticalError += verticalIncrement;
                error += verticalError;
            }
            if (doubledError >= horizontalError || 2 * error > verticalError)
            {
                left++; right--;
                horizontalError += horizontalIncrement;
                error += horizontalError;
            }
        } while (left <= right);
        // Very narrow ellipses can reach their center column before their tips.
        while (lowerRow - upperRow < diameterY)
        {
            RecordRow(lowerRow++, left - 1, right + 1);
            RecordRow(upperRow--, left - 1, right + 1);
        }

        void RecordRow(int rowIndex, int boundaryLeft, int boundaryRight)
        {
            if (rowIndex < 0 || rowIndex >= rows.Length) return;
            Row row = rows[rowIndex];
            row.Left = Math.Min(row.Left, boundaryLeft);
            row.Right = Math.Max(row.Right, boundaryRight);
            row.BrushLeft = Math.Max(row.BrushLeft, boundaryLeft + 1);
            row.BrushRight = Math.Min(row.BrushRight, boundaryRight - 1);
        }
    }
}

using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>SRCCOPY is a ternary raster-operation encoding, distinct from the DC's binary ROP2.</summary>
    public const uint SourceCopy = 0x00CC0020;

    /// <summary>Copy an unscaled region between registered DCs. Each origin applies to its own coordinate space.</summary>
    public bool BitBlt(ushort destinationHdc, short destinationX, short destinationY, short width, short height,
        ushort sourceHdc, short sourceX, short sourceY, uint rasterOperation)
    {
        var operation = (BitmapRasterOperation)rasterOperation;
        if (operation is not (BitmapRasterOperation.SourceCopy or BitmapRasterOperation.SourceAnd or BitmapRasterOperation.BrushThroughSourceMask))
            throw new NotSupportedException($"Unsupported BitBlt raster operation {rasterOperation:X8}.");
        Win16DeviceContext destination = RequireDeviceContext(destinationHdc), source = RequireDeviceContext(sourceHdc);
        uint brush = operation == BitmapRasterOperation.BrushThroughSourceMask
            ? SelectedBrushColor(destination) ?? throw new NotSupportedException("Masked pattern blits require a solid brush.") : 0;
        int changed = destination.Surface.CopyRegion(source.Surface,
            destinationX - destination.WindowOrigin.X, destinationY - destination.WindowOrigin.Y, width, height,
            sourceX - source.WindowOrigin.X, sourceY - source.WindowOrigin.Y, operation, brush);
        RecordOperation("BitBlt", destinationHdc, new(destinationX, destinationY,
            unchecked((short)(destinationX + width)), unchecked((short)(destinationY + height))), changed);
        return true;
    }

    /// <summary>PATCOPY uses the selected brush, ignoring ROP2; signed extents normalize before clipping.</summary>
    public bool PatBlt(ushort hdc, short left, short top, short width, short height, uint operation)
    {
        if (operation != (uint)BitmapRasterOperation.PatternCopy)
            throw new NotSupportedException($"Unsupported PatBlt raster operation {operation:X8}; expected PATCOPY.");
        Win16DeviceContext context = RequireDeviceContext(hdc);
        uint? brush = SelectedBrushColor(context);
        int changed = brush is uint color ? context.Surface.PaintPattern(left - context.WindowOrigin.X,
            top - context.WindowOrigin.Y, width, height, color) : 0;
        RecordOperation("PatBlt", hdc, new(left, top, unchecked((short)(left + width)), unchecked((short)(top + height))), changed);
        return true;
    }
}

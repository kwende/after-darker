namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>SRCCOPY is a ternary raster-operation encoding, distinct from the DC's binary ROP2.</summary>
    public const uint SourceCopy = 0x00CC0020;

    /// <summary>Copy an unscaled region between registered DCs. Each origin applies to its own coordinate space.</summary>
    public bool BitBlt(ushort destinationHdc, short destinationX, short destinationY, short width, short height,
        ushort sourceHdc, short sourceX, short sourceY, uint rasterOperation)
    {
        if (rasterOperation != SourceCopy) throw new NotSupportedException($"BitBlt raster operation {rasterOperation:X8} is unsupported; expected SRCCOPY.");
        Win16DeviceContext destination = RequireDeviceContext(destinationHdc), source = RequireDeviceContext(sourceHdc);
        int changed = destination.Surface.CopyRegion(source.Surface,
            destinationX - destination.WindowOrigin.X, destinationY - destination.WindowOrigin.Y, width, height,
            sourceX - source.WindowOrigin.X, sourceY - source.WindowOrigin.Y);
        RecordOperation("BitBlt", destinationHdc, new(destinationX, destinationY,
            unchecked((short)(destinationX + width)), unchecked((short)(destinationY + height))), changed);
        return true;
    }
}

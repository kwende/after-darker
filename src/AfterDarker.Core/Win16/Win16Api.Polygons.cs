using System.Buffers.Binary;
using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Api
{
    /// <summary>Read a bounded array of signed POINT16 values before any drawing side effect.</summary>
    public bool Polygon(ushort hdc, FarPointer16 vertices, short count)
    {
        if (count < 2) return false;
        if (count > 256) throw new NotSupportedException("Polygon supports at most 256 vertices.");
        byte[] bytes = State.Memory.Read(vertices, count * 4);
        var points = new Point16[count];
        for (int index = 0; index < count; index++)
            points[index] = new(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4)),
                BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4 + 2)));
        return Drawing.Polygon(hdc, points);
    }
}

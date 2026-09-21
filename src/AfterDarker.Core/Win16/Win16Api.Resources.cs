using System.Text;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Api
{
    /// <summary>Load RT_BITMAP by ordinal or checked guest ANSI name and create a guest-owned bitmap.</summary>
    /// <remarks>MAKEINTRESOURCE encodes the numeric ID with selector zero; it must not be dereferenced.
    /// Every successful load owns a fresh bitmap, released through DeleteObject. See docs/ne-bitmap-resources.md.</remarks>
    public ushort LoadBitmap(ushort instance, FarPointer16 name)
    {
        Win16ModuleResources resources = State.ModuleResources ?? throw new NotSupportedException("No module resource catalog was supplied.");
        if (instance != resources.Instance) return 0;
        NeResourceIdentifier identifier;
        if (name.Selector == 0) identifier = new(name.Offset, null);
        else
        {
            var bytes = new List<byte>();
            for (int index = 0; ; index++)
            {
                if (index == 255 || (long)name.Offset + index >= 65536)
                    throw new InvalidDataException("Resource name is not terminated within the supported guest bounds.");
                byte character = State.Memory.Read(new(name.Selector, (ushort)(name.Offset + index)), 1)[0];
                if (character == 0) break;
                bytes.Add(character);
            }
            string text = Encoding.Latin1.GetString(bytes.ToArray());
            identifier = text.StartsWith('#') && ushort.TryParse(text.AsSpan(1), out ushort number)
                ? new(number, null) : new(null, text);
        }
        byte[]? payload = resources.Catalog.Find(new(NeFormat.ResourceTypes.Bitmap, null), identifier);
        return payload is null ? (ushort)0 : Drawing.CreateBitmap(DibBitmapDecoder.Decode(payload));
    }
}

namespace AfterDarker.Core.Ne;

/// <summary>
/// Values encoded by the NE file format. Keep these separate from our chosen
/// memory layout: a file dictates how to interpret a reference, not where we load it.
/// Raw model fields remain bytes/words so unsupported values are still inspectable.
/// </summary>
public static class NeFormat
{
    public static class HeaderFlags
    {
        public const ushort DataModeMask = 0x0003;
        public const ushort SingleData = 0x0001;
        public const ushort Win32 = 0x0010;
        public const ushort SelfLoading = 0x0800;
        public const ushort LinkErrors = 0x2000;
        public const ushort Library = 0x8000;
    }

    public static class RelocationFlags
    {
        // The low two bits select a kind; other bits modify its behavior.
        public const byte KindMask = 0x03;
        public const byte Internal = 0;
        public const byte ImportByOrdinal = 1;
        public const byte ImportByName = 2;
        public const byte OsFixup = 3;
        public const byte Additive = 0x04;
    }

    public static class AddressTypes
    {
        // These are format tags, NOT byte counts. FarPointer16 has tag 3
        // but occupies four bytes: a two-byte offset followed by a selector.
        public const byte Selector16 = 2;
        public const byte FarPointer16 = 3;
        public const byte Offset16 = 5;
    }

    public const ushort EndOfRelocationChain = 0xFFFF;
    public const ushort InternalEntryOrdinalMarker = 0x00FF;
    public const ushort MaximumFixedSegmentNumber = 0x00FE;
}

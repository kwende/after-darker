using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Turns source-ordered Pascal words into typed C# arguments without accessing the CPU.</summary>
/// <remarks>
/// The stack decoder has already reversed the order of pushed words. A far pointer therefore
/// arrives as selector then offset, and a DWORD as high word then low word. These pairs are
/// each one API parameter, even though they occupy two words. See docs/runtime-code-map.md.
/// </remarks>
internal sealed class Win16ArgumentReader(IReadOnlyList<ushort> words)
{
    private int nextWordIndex;

    /// <summary>Read a handle, flag, or unsigned 16-bit value.</summary>
    public ushort ReadWord() => words[nextWordIndex++];

    /// <summary>Interpret the next word as a signed coordinate or integer without changing its bits.</summary>
    public short ReadSignedWord() => unchecked((short)ReadWord());

    /// <summary>Combine selector and offset words into a guest address, never a native pointer.</summary>
    public FarPointer16 ReadFarPointer()
    {
        ushort selector = ReadWord();
        ushort offset = ReadWord();
        return new FarPointer16(selector, offset);
    }

    /// <summary>Combine high and low words into one 32-bit value such as COLORREF.</summary>
    public uint ReadDoubleWord()
    {
        ushort highWord = ReadWord();
        ushort lowWord = ReadWord();
        return ((uint)highWord << 16) | lowWord;
    }

    /// <summary>Read a by-value POINT. Pascal pushes its high word (Y) before its low word (X).</summary>
    /// <remarks>Memory stores X then Y; our source-ordered stack words are Y then X. See docs/win16-implementations.md.</remarks>
    public Point16 ReadPoint()
    {
        short y = ReadSignedWord();
        short x = ReadSignedWord();
        return new Point16(x, y);
    }
}

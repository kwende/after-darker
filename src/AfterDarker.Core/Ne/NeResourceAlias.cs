using System.Buffers.Binary;
using System.Text;

namespace AfterDarker.Core.Ne;

/// <summary>An RT_NAMETABLE entry maps optional textual identities to the directory's numeric identities.</summary>
/// <param name="Type">Identity callers use for the resource type.</param>
/// <param name="Id">Identity callers use for the resource itself.</param>
/// <param name="TypeNumber">Numeric resource-directory type.</param>
/// <param name="IdNumber">Numeric resource-directory ID.</param>
public sealed record NeResourceAlias(NeResourceIdentifier Type, NeResourceIdentifier Id, ushort TypeNumber, ushort IdNumber)
{
    private const ushort NamedAliasFlag = 0x8000, NumericIdentityMask = 0x7FFF;
    private const int TypeOffset = 2, IdOffset = 4, NamesOffset = 6;
    /// <summary>Read bounded variable-length Win16 RT_NAMETABLE records, including their two terminated names.</summary>
    /// <remarks>The high bit here means a name alias, unlike its numeric-ID meaning in the NE directory.
    /// See docs/ne-bitmap-resources.md and Wine's NE_FindNameTableId for the independently implemented format.</remarks>
    internal static IReadOnlyList<NeResourceAlias> Decode(ReadOnlySpan<byte> payload)
    {
        var entries = new List<NeResourceAlias>();
        int offset = 0;
        while (offset + 2 <= payload.Length)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
            if (length == 0) return entries.AsReadOnly();
            if (length < 8 || length > payload.Length - offset)
                throw new InvalidDataException("Invalid RT_NAMETABLE record length.");
            ReadOnlySpan<byte> record = payload.Slice(offset, length);
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(record[TypeOffset..]);
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(record[IdOffset..]);
            int nextString = NamesOffset;
            string typeName = ReadName(record, ref nextString);
            string resourceName = ReadName(record, ref nextString);
            entries.Add(new(Identity(type, typeName), Identity(id, resourceName),
                (ushort)(type & NumericIdentityMask), (ushort)(id & NumericIdentityMask)));
            offset += length;
        }
        throw new InvalidDataException("RT_NAMETABLE has no terminating zero-length record.");
    }

    private static NeResourceIdentifier Identity(ushort value, string name) =>
        (value & NamedAliasFlag) == 0 ? new(value, null) : new(null, name);

    private static string ReadName(ReadOnlySpan<byte> record, ref int offset)
    {
        int length = record[offset..].IndexOf((byte)0);
        if (length < 0) throw new InvalidDataException("Unterminated RT_NAMETABLE name.");
        string name = Encoding.Latin1.GetString(record.Slice(offset, length));
        offset += length + 1;
        return name;
    }
}

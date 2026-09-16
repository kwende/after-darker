using AfterDarker.Core.Ne;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class NeReaderTests
{
    [TestMethod]
    public void HeaderAndSegments_KeepFileOffsetsSeparateFromGuestAddresses()
    {
        NeImage image = NeReader.Read(NeFixture.Create());
        Assert.AreEqual(0x80, image.Header.FileOffset);
        Assert.IsTrue(image.Header.IsLibrary);
        Assert.AreEqual(new NeAddress(1, 8), image.Header.Startup);
        Assert.AreEqual((ushort)2, image.Header.AutomaticDataSegment);
        Assert.AreEqual((ushort)1024, image.Header.HeapBytes);
        Assert.AreEqual(0x400, image.Segments[0].FileOffset);
        Assert.AreEqual(64, image.Segments[0].FileBytes);
        Assert.IsNull(image.Segments[1].FileOffset);
        Assert.AreEqual(0, image.Segments[1].FileBytes);
        Assert.AreEqual(256, image.Segments[1].MinimumAllocationBytes);
        Assert.IsTrue(image.Segments[1].IsData);
    }

    [TestMethod]
    public void Entries_PreserveOrdinalHolesMovableAddressesAndConstants()
    {
        NeImage image = NeReader.Read(NeFixture.Create());
        CollectionAssert.AreEqual(new ushort[] { 1, 3, 4 }, image.Entries.Select(e => e.Ordinal).ToArray());
        Assert.AreEqual(new NeAddress(1, 0x10), image.FindExport("module")!.Address);
        NeEntry movable = image.FindExport("FRAME_ALIAS")!;
        Assert.IsTrue(movable.Movable);
        Assert.AreEqual(new NeAddress(1, 0x20), movable.Address);
        Assert.IsNull(image.Entries[2].Address);
        Assert.AreEqual((ushort)0xBEEF, image.Entries[2].ConstantValue);
        Assert.IsNull(image.FindExport("TESTLIB")); // Ordinal zero is module identity.
        Assert.IsFalse(image.Names.Single(n => n.Text == "FRAME_ALIAS").Resident);
    }

    [TestMethod]
    public void Imports_DeduplicateIdentitiesButRetainEveryFixupRecord()
    {
        NeImage image = NeReader.Read(NeFixture.Create());
        Assert.AreEqual(2, image.Imports.Count);
        Assert.AreEqual(3, image.Relocations.Count);
        Assert.AreEqual(new NeImport("KERNEL", 18, null), image.Imports[0]);
        Assert.AreEqual(new NeImport("KERNEL", null, "NamedProc"), image.Imports[1]);
        Assert.AreEqual((ushort)4, image.Relocations[0].SourceOffset);
        Assert.AreEqual(0x442, image.Relocations[0].RecordFileOffset);
        Assert.AreEqual((byte)3, image.Relocations[0].AddressType);
        Assert.IsFalse(image.Relocations[0].Additive);
    }

    [TestMethod]
    public void Resources_ResolveNumericAndNamedIdentifiersUsingResourceTableBase()
    {
        NeImage image = NeReader.Read(NeFixture.Create());
        Assert.AreEqual(2, image.Resources.Count);
        Assert.AreEqual(new NeResourceIdentifier(2, null), image.Resources[0].Type);
        Assert.AreEqual(new NeResourceIdentifier(7, null), image.Resources[0].Id);
        Assert.AreEqual(0x500, image.Resources[0].FileOffset);
        Assert.AreEqual(32, image.Resources[0].Length);
        Assert.AreEqual(new NeResourceIdentifier(null, "CUSTOM"), image.Resources[1].Type);
        Assert.AreEqual(new NeResourceIdentifier(null, "FRAME0"), image.Resources[1].Id);
        Assert.AreEqual(16, image.Resources[1].Length);
    }

    [TestMethod]
    public void NoStartupOrResources_IsRepresentedExplicitly()
    {
        byte[] b = NeFixture.Create();
        NeFixture.Word(b, 0x96, 0);
        NeFixture.Word(b, 0xA4, 0xA0); // Resource table equals resident table: absent.
        NeImage image = NeReader.Read(b);
        Assert.IsNull(image.Header.Startup);
        Assert.AreEqual(0, image.Resources.Count);
    }

    [TestMethod]
    public void ZeroSegmentLengthAndAllocationWords_Mean64KiBWhenApplicable()
    {
        byte[] b = NeFixture.Create();
        Array.Resize(ref b, 0x10400);
        NeFixture.Word(b, 0xC2, 0); NeFixture.Word(b, 0xC4, 0); NeFixture.Word(b, 0xC6, 0);
        NeFixture.Word(b, 0xCE, 0);
        NeImage image = NeReader.Read(b);
        Assert.AreEqual(65536, image.Segments[0].FileBytes);
        Assert.AreEqual(65536, image.Segments[0].MinimumAllocationBytes);
        Assert.AreEqual(0, image.Segments[1].FileBytes);
        Assert.AreEqual(65536, image.Segments[1].MinimumAllocationBytes);
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(63)] [DataRow(0xA0)]
    [DataRow(0x190)] [DataRow(0x20D)] [DataRow(0x450)] [DataRow(0x51F)]
    public void TruncatedInputs_FailWithAFormatDiagnostic(int length)
    {
        byte[] b = NeFixture.Create()[..length];
        InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() => NeReader.Read(b));
        StringAssert.Contains(error.Message, "NE");
    }

    [TestMethod]
    [DataRow(0, 0)] // MZ signature
    [DataRow(0x80, 0x4550)] // PE instead of NE
    [DataRow(0x96, 3)] // Startup segment out of bounds
    [DataRow(0x8E, 3)] // Automatic data segment out of bounds
    [DataRow(0xB2, 32)] // Segment alignment overflow
    [DataRow(0xD0, 32)] // Resource alignment overflow
    [DataRow(0xD4, 65535)] // Resource records outside their table
    [DataRow(0xF4, 0x70)] // Resource string outside its table
    [DataRow(0xDA, 0xFFFF)] // Resource payload outside file
    [DataRow(0x446, 2)] // Import module index out of bounds
    [DataRow(0x183, 0x100)] // Entry offset outside segment
    [DataRow(0x18A, 0)] // Missing movable marker
    [DataRow(0x20C, 2)] // Name references unused ordinal
    [DataRow(0x86, 20)] // Entry terminator omitted by declared size
    public void MalformedMetadata_IsRejected(int offset, int value)
    {
        byte[] b = NeFixture.Create();
        NeFixture.Word(b, offset, (ushort)value);
        Assert.ThrowsExactly<InvalidDataException>(() => NeReader.Read(b));
    }

    [TestMethod]
    public void HugeHeaderPointer_IsRejectedWithoutIntegerOverflow()
    {
        byte[] b = NeFixture.Create(); NeFixture.Dword(b, 0x3C, uint.MaxValue);
        Assert.ThrowsExactly<InvalidDataException>(() => NeReader.Read(b));
    }

    [TestMethod]
    [DataRow(true)] [DataRow(false)]
    public void UnsupportedVariants_AreExplicit(bool iterated)
    {
        byte[] b = NeFixture.Create();
        if (iterated) NeFixture.Word(b, 0xC4, 0x108); else b[0xB6] = 1;
        Assert.ThrowsExactly<NotSupportedException>(() => NeReader.Read(b));
    }
}
